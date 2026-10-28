using System.Net.Http.Json;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Application.Core.Notifications;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed class PushRetentionApiTests(WorkspaceApiFactory factory) : IClassFixture<WorkspaceApiFactory>
{
    [PostgreSqlFact]
    public async Task Retention_closes_concluded_days_and_removes_only_finished_details_without_touching_preferences_devices_or_enabling_resends()
    {
        var year = FantacalcioTime.ActualYear;
        using var owner = await factory.AccountAsync(globalAdmin: true);
        var account = (await owner.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var group = (await (await owner.PostAsJsonAsync("/api/workspace/groups", new CreateGroupRequest("Push retention"))).Content.ReadFromJsonAsync<GroupSummaryDto>())!.GroupId;
        var basket = (await (await owner.PostAsJsonAsync($"/api/workspace/groups/{group}/baskets", new CreateBasketRequest("Basket", year))).Content.ReadFromJsonAsync<BasketCreatedDto>())!.BasketId;
        var league = (await (await owner.PostAsJsonAsync($"/api/workspace/groups/{group}/leagues", new CreateLeagueRequest("League", year, [basket]))).Content.ReadFromJsonAsync<LeagueCreatedDto>())!.LeagueId;
        var team = (await (await owner.PostAsJsonAsync($"/api/workspace/groups/{group}/teams", new CreateTeamRequest(basket, year, "Push team", account.Id, []))).Content.ReadFromJsonAsync<TeamCreatedDto>())!.TeamId;
        (await owner.PatchAsJsonAsync("/api/push-notifications", new PushPreferencesPatch(EndDay: true))).EnsureSuccessStatusCode();
        var now = DateTimeOffset.UtcNow;
        Guid Delivery(FantasoccerDbContextHolder holder, int day, PushDeliveryStatus status, DateTimeOffset expires, DateTimeOffset? completed, DateTimeOffset? lease = null, bool teamScope = false)
        {
            var row = new PushDelivery { Id = Guid.NewGuid(), DeduplicationKey = Guid.NewGuid().ToString("N"), SeasonIndex = year, Day = day, UserId = account.Id, GroupId = group,
                LeagueId = league, TeamId = teamScope ? team : null, BasketId = teamScope ? basket : null, Kind = day == 0 ? 4 : 3, Title = "FantaZone", Body = "body", Uri = "/live",
                Status = status, ExpiresAtUtc = expires, CompletedAtUtc = completed, LeaseId = lease is null ? null : Guid.NewGuid(), LeaseUntilUtc = lease, NextAttemptAtUtc = now };
            holder.Database.PushDeliveries.Add(row);
            return row.Id;
        }
        Guid done, pendingFuture, leased, expiredPending, confirmed, marketOld, marketRecent, recentDay, quarantinedFresh;
        await using (var database = factory.Database.CreateContext())
        {
            var holder = new FantasoccerDbContextHolder(database);
            var club = new FootballClub { Name = "Retention club", Abbreviation = "rtc" }; var rival = new FootballClub { Name = "Retention rival", Abbreviation = "rtr" };
            var person = new FootballPlayer { Name = "Retention player " + Guid.NewGuid().ToString("N") };
            database.Clubs.AddRange(club, rival);
            database.PlayerSeasons.Add(new() { Player = person, Club = club, SeasonIndex = year, NormalizedName = KeyUtils.GetPlayerKey(person.Name), Position = PlayerPosition.Forward });
            foreach (var (day, kickoff) in new[] { (5, now.AddDays(-5)), (6, now.AddHours(-4)), (7, now.AddHours(5)) })
                database.FootballMatchdays.Add(new() { SeasonIndex = year, SerieADay = day, Matches = [new() { SeasonIndex = year, SerieADay = day, HomeClub = club, AwayClub = rival,
                    KickoffUtc = kickoff, IsFinished = day < 7 }] });
            database.PushDayCheckpoints.Add(new() { SeasonIndex = year, Day = 5, HasMatchEventVotes = true, MatchEventSequence = 4, Votes = [new() { Ordinal = 0, PlayerId = person.Id, Goals = 1 }] });
            database.PushDayCheckpoints.Add(new() { SeasonIndex = year, Day = 6, EndDay = false });
            database.FootballVotes.Add(new() { SeasonIndex = year, Day = 5, Source = FootballVoteSource.Notification, Player = person, Club = club, Value = 6 });
            database.FootballVotes.Add(new() { SeasonIndex = year, Day = 5, Source = FootballVoteSource.Official, Player = person, Club = club, Value = 6, IsFinal = true, HasVote = true });
            database.PushFormationChangeReceipts.Add(new() { GroupId = group, BasketId = basket, TeamId = team, SeasonIndex = year, Day = 5, LeagueId = league, ChangeOrdinal = 0, RecordedAtUtc = now.AddDays(-5) });
            database.PushFormationChangeReceipts.Add(new() { GroupId = group, BasketId = basket, TeamId = team, SeasonIndex = year, Day = 6, LeagueId = league, ChangeOrdinal = 0, RecordedAtUtc = now.AddHours(-2) });
            database.PushDeviceBindings.Add(new() { EndpointHash = "retention-device", UserId = account.Id, Endpoint = "https://push.example.test/retention", P256dh = "key", Auth = "auth", IsValid = true });
            done = Delivery(holder, 5, PushDeliveryStatus.Completed, now.AddDays(-3), now.AddDays(-4), teamScope: true);
            pendingFuture = Delivery(holder, 5, PushDeliveryStatus.Pending, now.AddHours(1), null);
            leased = Delivery(holder, 5, PushDeliveryStatus.Pending, now.AddDays(-3), null, now.AddMinutes(5));
            expiredPending = Delivery(holder, 5, PushDeliveryStatus.Pending, now.AddDays(-3), null);
            confirmed = Delivery(holder, 5, PushDeliveryStatus.Completed, now.AddDays(-3), now.AddDays(-4));
            quarantinedFresh = Delivery(holder, 5, PushDeliveryStatus.Quarantined, now.AddDays(1), null);
            marketOld = Delivery(holder, 0, PushDeliveryStatus.Completed, now.AddDays(-3), now.AddDays(-4));
            marketRecent = Delivery(holder, 0, PushDeliveryStatus.Completed, now.AddDays(1), now.AddHours(-1));
            recentDay = Delivery(holder, 6, PushDeliveryStatus.Completed, now.AddDays(1), now.AddHours(-1));
            await database.SaveChangesAsync();
            (await database.PushDeliveries.SingleAsync(x => x.Id == done)).Receipts.Add(new() { Ordinal = 0, EndpointHash = "retention-device", Outcome = "ProviderAccepted" });
            database.PushDeliveryScopeConfirmations.Add(new() { Id = Guid.NewGuid(), DeliveryId = confirmed, ActorUserId = account.Id, GroupId = group, LeagueId = league, SeasonIndex = year, Uri = "/live", RecordedAtUtc = now.AddDays(-4) });
            await database.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<PushRetentionService>().CleanupAsync(year);
        await using (var database = factory.Database.CreateContext())
        {
            var remaining = await database.PushDeliveries.Where(x => x.UserId == account.Id).Select(x => x.Id).ToArrayAsync();
            Assert.Equal(new[] { pendingFuture, leased, quarantinedFresh, marketRecent, recentDay }.Order(), remaining.Order());
            Assert.False(await database.PushDeliveryScopeConfirmations.AnyAsync(x => x.DeliveryId == confirmed));
            var closed = await database.PushDayCheckpoints.AsSplitQuery().SingleAsync(x => x.SeasonIndex == year && x.Day == 5);
            Assert.NotNull(closed.ClosedAtUtc); Assert.Empty(closed.Votes); Assert.Equal(4, closed.MatchEventSequence);
            Assert.Null((await database.PushDayCheckpoints.SingleAsync(x => x.SeasonIndex == year && x.Day == 6)).ClosedAtUtc);
            Assert.False(await database.PushFormationChangeReceipts.AnyAsync(x => x.SeasonIndex == year && x.Day == 5));
            Assert.True(await database.PushFormationChangeReceipts.AnyAsync(x => x.SeasonIndex == year && x.Day == 6));
            // Push baselines go; official votes, preferences and active devices stay.
            Assert.False(await database.FootballVotes.AnyAsync(x => x.SeasonIndex == year && x.Day == 5 && x.Source == FootballVoteSource.Notification));
            Assert.True(await database.FootballVotes.AnyAsync(x => x.SeasonIndex == year && x.Day == 5 && x.Source == FootballVoteSource.Official));
            Assert.True((await database.UserPushPreferences.SingleAsync(x => x.UserId == account.Id)).EndDay);
            Assert.True(await database.PushDeviceBindings.AnyAsync(x => x.EndpointHash == "retention-device" && x.IsActive));
        }

        // Day 6 ended about two hours ago: with a 1 h window it is closed now, and the
        // producer (whose "last day" window still covers it) cannot resend it.
        await using (var database = factory.Database.CreateContext())
        {
            var game = Guid.NewGuid();
            database.FantasyCalendars.Add(new() { GroupId = group, LeagueId = league, SeasonIndex = year, Stages = [new() { Stage = "@", GroupId = group, LeagueId = league, SeasonIndex = year,
                Days = [new() { Stage = "@", GroupId = group, LeagueId = league, SeasonIndex = year, Ordinal = 0, FantasyTurn = 6, SerieADay = 6, Games = [new() { GroupId = group, LeagueId = league,
                    SeasonIndex = year, Id = game, Stage = "@", DayOrdinal = 0, HomeTeamId = team, HomeBasketId = basket, HomeName = "Push team", AwayName = "Rest", HomeOwner = account.Email,
                    Result = new() { HomeGoals = 2, AwayGoals = 1, Home = new() { Value = 70 }, Away = new() { Value = 60 } } }] }] }] });
            await database.SaveChangesAsync();
            await new PushRetentionService(database, Options.Create(new PushRetentionOptions { Retention = TimeSpan.FromHours(1) }), TimeProvider.System).CleanupAsync(year);
        }
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<MatchdayPushProducer>().RunAsync(year);
        await using (var database = factory.Database.CreateContext())
        {
            Assert.NotNull((await database.PushDayCheckpoints.SingleAsync(x => x.SeasonIndex == year && x.Day == 6)).ClosedAtUtc);
            Assert.False(await database.PushDeliveries.AnyAsync(x => x.SeasonIndex == year && x.Day == 6 && x.Kind == 3 && x.Id != recentDay));
            Assert.False(await database.PushFormationChangeReceipts.AnyAsync(x => x.SeasonIndex == year && x.Day == 6));
        }
    }

    private sealed record FantasoccerDbContextHolder(Fantasoccer.Infrastructure.PostgreSql.FantasoccerDbContext Database);
}

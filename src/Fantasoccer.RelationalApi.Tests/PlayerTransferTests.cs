using System.Net;
using System.Net.Http.Json;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Football;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Application.Core.Operations;
using Fantasoccer.Business;
using Fantasoccer.Business.IngestionData;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed class PlayerTransferTests(WorkspaceApiFactory factory) : IClassFixture<WorkspaceApiFactory>
{
    private const string Refresh = "/api/football/refresh";

    [PostgreSqlFact]
    public async Task Nightly_catalogue_tracks_arrivals_moves_confirmed_departures_and_returns_without_rewriting_history()
    {
        var year = FantacalcioTime.ActualYear;
        var clock = new ManualClock(new DateTimeOffset(2027, 1, 9, 1, 30, 0, TimeSpan.Zero)); // 02:30 in Rome
        var catalogue = new Catalogue();
        catalogue.Set(("Transfer keeper", "Napoli", Role.GoalKeeper), ("Transfer mover", "Napoli", Role.Forward), ("Transfer leaver", "Fiorentina", Role.Defensor));
        using var admin = await factory.AccountAsync(globalAdmin: true);
        var email = admin.DefaultRequestHeaders.GetValues("X-Workspace-User").Single();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddSingleton<IAllPlayers>(catalogue); services.AddSingleton<TimeProvider>(clock);
        }));
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("X-Workspace-User", email); client.DefaultRequestHeaders.Add("X-Workspace-Admin", "true");
        app.Services.GetRequiredService<IOptions<GlobalAdministratorOptions>>().Value.Emails = [email];
        async Task RunAsync() => (await client.PostAsJsonAsync(Refresh, new RefreshFootballRequest("catalogue", year))).EnsureSuccessStatusCode();
        async Task<PlayerSeason> SeasonAsync(string name)
        {
            await using var database = factory.Database.CreateContext();
            return await database.PlayerSeasons.AsNoTracking().Include(x => x.Player).SingleAsync(x => x.SeasonIndex == year && x.Player.Name == name);
        }

        // Initial snapshot of the season: a starting picture, not arrivals.
        await RunAsync();
        foreach (var name in new[] { "Transfer keeper", "Transfer mover", "Transfer leaver" })
        {
            var row = await SeasonAsync(name);
            Assert.Null(row.TransferredAtUtc); Assert.Null(row.PreviousClubId); Assert.True(row.IsActive);
        }

        // A fantasy team owns the leaver and the mover.
        var group = (await Read<GroupSummaryDto>(await admin.PostAsJsonAsync("/api/workspace/groups", new CreateGroupRequest("Transfers")))).GroupId;
        var me = (await admin.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var basket = (await Read<BasketCreatedDto>(await admin.PostAsJsonAsync($"/api/workspace/groups/{group}/baskets", new CreateBasketRequest("Serie A", year)))).BasketId;
        var team = (await Read<TeamCreatedDto>(await admin.PostAsJsonAsync($"/api/workspace/groups/{group}/teams", new CreateTeamRequest(basket, year, "Owners", me.Id, [])))).TeamId;
        await using (var database = factory.Database.CreateContext())
        {
            var roster = await database.RosterSnapshots.Include(x => x.Players).SingleAsync(x => x.GroupId == group && x.TeamId == team && x.Kind == RosterKind.Season);
            foreach (var (name, index) in new[] { "Transfer leaver", "Transfer mover" }.Select((name, index) => (name, index)))
            {
                var season = await database.PlayerSeasons.Include(x => x.Player).SingleAsync(x => x.SeasonIndex == year && x.Player.Name == name);
                roster.Players.Add(new() { Ordinal = index, PlayerId = season.PlayerId, ClubId = season.ClubId, PlayerRole = (int)season.Position,
                    IsActive = true, IsVisible = true, Price = 10, Status = 0, Position = 11 });
            }
            await database.SaveChangesAsync();
        }

        // Night 2: a January move, a new arrival from abroad, a first absence.
        clock.Advance(TimeSpan.FromDays(1));
        var night2 = clock.GetUtcNow();
        catalogue.Set(("Transfer keeper", "Napoli", Role.GoalKeeper), ("Transfer mover", "Fiorentina", Role.Forward), ("Transfer arrival", "Napoli", Role.Midfielder));
        await RunAsync();
        var mover = await SeasonAsync("Transfer mover");
        Assert.Equal("fio", mover.ClubId); Assert.Equal("nap", mover.PreviousClubId); Assert.Equal(night2, mover.TransferredAtUtc);
        var arrival = await SeasonAsync("Transfer arrival");
        Assert.Equal("nap", arrival.ClubId); Assert.Null(arrival.PreviousClubId); Assert.Equal(night2, arrival.TransferredAtUtc);
        var leaver = await SeasonAsync("Transfer leaver");
        Assert.True(leaver.IsActive); Assert.Equal("fio", leaver.ClubId); Assert.Equal(night2, leaver.MissingSinceUtc);
        Assert.Null(leaver.TransferredAtUtc);
        Assert.Null((await SeasonAsync("Transfer keeper")).TransferredAtUtc);

        // A manual rerun the same evening does not confirm the departure.
        clock.Advance(TimeSpan.FromHours(10));
        await RunAsync();
        leaver = await SeasonAsync("Transfer leaver");
        Assert.True(leaver.IsActive); Assert.Equal("fio", leaver.ClubId);

        // Night 3: still missing, the departure is confirmed and dated to its discovery.
        clock.Advance(TimeSpan.FromHours(14));
        await RunAsync();
        leaver = await SeasonAsync("Transfer leaver");
        Assert.False(leaver.IsActive); Assert.Null(leaver.ClubId); Assert.Equal("fio", leaver.PreviousClubId);
        Assert.Equal(night2, leaver.TransferredAtUtc); Assert.Null(leaver.MissingSinceUtc);
        Assert.Equal("fio", (await SeasonAsync("Transfer mover")).ClubId); // a move is not repeated every night

        // The fantasy roster keeps the departed player, with his last club, flagged inactive;
        // the moved player follows his new club.
        await using (var database = factory.Database.CreateContext())
        {
            var roster = await database.RosterSnapshots.AsNoTracking().Include(x => x.Players).ThenInclude(x => x.Player)
                .SingleAsync(x => x.GroupId == group && x.TeamId == team && x.Kind == RosterKind.Season);
            var kept = roster.Players.Single(x => x.Player.Name == "Transfer leaver");
            Assert.False(kept.IsActive); Assert.Equal("fio", kept.ClubId);
            Assert.Equal("fio", roster.Players.Single(x => x.Player.Name == "Transfer mover").ClubId);
        }
        var list = (await client.GetFromJsonAsync<PlayersDto<FootballPlayerDto>>($"/api/football/{year}/players"))!.Players;
        var listed = list.Single(x => x.Name == "Transfer leaver");
        Assert.True(listed.LeftSerieA); Assert.Equal("fio", listed.RealTeam!.ClubId); Assert.Equal("fio", listed.PreviousTeam!.ClubId); Assert.Equal(night2, listed.TransferredAtUtc);
        var moved = list.Single(x => x.Name == "Transfer mover");
        Assert.False(moved.LeftSerieA); Assert.Equal("fio", moved.RealTeam!.ClubId); Assert.Equal("nap", moved.PreviousTeam!.ClubId);
        // Out of Serie A: excluded from new auction orders (they require a current club).
        await using (var database = factory.Database.CreateContext())
            Assert.False(await database.PlayerSeasons.AnyAsync(x => x.SeasonIndex == year && x.ClubId != null && x.PlayerId == leaver.PlayerId));

        // Night 4: he returns to Serie A in another club.
        clock.Advance(TimeSpan.FromDays(1));
        var night4 = clock.GetUtcNow();
        catalogue.Set(("Transfer keeper", "Napoli", Role.GoalKeeper), ("Transfer mover", "Fiorentina", Role.Forward), ("Transfer arrival", "Napoli", Role.Midfielder),
            ("Transfer leaver", "Napoli", Role.Defensor));
        await RunAsync();
        leaver = await SeasonAsync("Transfer leaver");
        Assert.True(leaver.IsActive); Assert.Equal("nap", leaver.ClubId); Assert.Equal("fio", leaver.PreviousClubId); Assert.Equal(night4, leaver.TransferredAtUtc);
    }

    [Fact]
    public void Departures_recorded_before_tracking_are_out_of_Serie_A_with_an_unknown_date()
    {
        var now = DateTimeOffset.UtcNow;
        var legacy = new PlayerSeason { PlayerId = "legacy", ClubId = "nap", IsActive = false };
        PlayerTransferRules.Missing(legacy, now);
        Assert.Null(legacy.ClubId); Assert.Equal("nap", legacy.PreviousClubId); Assert.Null(legacy.TransferredAtUtc);
        var noClub = new PlayerSeason { PlayerId = "unknown", ClubId = null, IsActive = true };
        PlayerTransferRules.Missing(noClub, now);
        Assert.False(noClub.IsActive); Assert.Null(noClub.PreviousClubId);
        // Returning to the very club he left: no misleading previous club.
        var back = new PlayerSeason { PlayerId = "back", ClubId = null, PreviousClubId = "nap", TransferredAtUtc = now.AddDays(-20), IsActive = false };
        PlayerTransferRules.Listed(back, "nap", isNewRow: false, initialSnapshot: false, now);
        Assert.Equal("nap", back.ClubId); Assert.Null(back.PreviousClubId); Assert.Equal(now, back.TransferredAtUtc);
    }

    [Fact]
    public void The_catalogue_runs_every_night_at_half_past_two_in_Rome()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");
        foreach (var now in new[] { new DateTimeOffset(2027, 1, 15, 12, 0, 0, TimeSpan.Zero), new DateTimeOffset(2027, 7, 15, 12, 0, 0, TimeSpan.Zero) })
        {
            var next = TimeZoneInfo.ConvertTime(SqlJobSchedule.Next("catalogue", now), zone);
            Assert.Equal((2, 30), (next.Hour, next.Minute));
            Assert.Equal(now.Date.AddDays(1), next.Date);
        }
    }

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    internal sealed class Catalogue : IAllPlayers
    {
        private List<RealPlayer> _players = [];
        // The rest of a realistic list: a few changes never look like a mass departure.
        public int Fillers { get; init; } = 20;
        public void Set(params (string Name, string Club, Role Role)[] players) =>
            _players = players.Select(x => new RealPlayer { Name = x.Name, RealTeam = new() { Name = x.Club }, Role = x.Role, IsActive = true, Visible = true })
                .Concat(Enumerable.Range(0, Fillers).Select(index => new RealPlayer { Name = $"Filler player {(char)('a' + index / 26)}{(char)('a' + index % 26)}",
                    RealTeam = new() { Name = "Juventus" }, Role = Role.Midfielder, IsActive = true, Visible = true })).ToList();
        public Task<List<RealPlayer>> GetAsync() => Task.FromResult(_players.ToList());
    }
}

public sealed class PlayerTransferGuardTests(WorkspaceApiFactory factory) : IClassFixture<WorkspaceApiFactory>
{
    [PostgreSqlFact]
    public async Task A_player_listed_again_the_next_night_never_leaves_and_a_missing_club_aborts_the_run()
    {
        var year = FantacalcioTime.ActualYear;
        var clock = new ManualClock(new DateTimeOffset(2027, 2, 1, 1, 30, 0, TimeSpan.Zero));
        var catalogue = new PlayerTransferTests.Catalogue { Fillers = 0 };
        var napoli = Enumerable.Range(0, 6).Select(index => ($"Glitch napoli {(char)('a' + index)}", "Napoli", Role.Midfielder)).ToArray();
        catalogue.Set([.. napoli, ("Glitch fiorentina", "Fiorentina", Role.Forward)]);
        using var admin = await factory.AccountAsync(globalAdmin: true);
        var email = admin.DefaultRequestHeaders.GetValues("X-Workspace-User").Single();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddSingleton<IAllPlayers>(catalogue); services.AddSingleton<TimeProvider>(clock);
        }));
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("X-Workspace-User", email); client.DefaultRequestHeaders.Add("X-Workspace-Admin", "true");
        app.Services.GetRequiredService<IOptions<GlobalAdministratorOptions>>().Value.Emails = [email];
        Task<HttpResponseMessage> RunAsync() => client.PostAsJsonAsync("/api/football/refresh", new RefreshFootballRequest("catalogue", year));
        (await RunAsync()).EnsureSuccessStatusCode();

        // One night the provider page lacks a player; the next night he is back.
        clock.Advance(TimeSpan.FromDays(1));
        catalogue.Set([.. napoli.Skip(1), ("Glitch fiorentina", "Fiorentina", Role.Forward)]);
        (await RunAsync()).EnsureSuccessStatusCode();
        clock.Advance(TimeSpan.FromDays(1));
        catalogue.Set([.. napoli, ("Glitch fiorentina", "Fiorentina", Role.Forward)]);
        (await RunAsync()).EnsureSuccessStatusCode();
        await using (var database = factory.Database.CreateContext())
        {
            var row = await database.PlayerSeasons.Include(x => x.Player).SingleAsync(x => x.SeasonIndex == year && x.Player.Name == "Glitch napoli a");
            Assert.True(row.IsActive); Assert.Equal("nap", row.ClubId); Assert.Null(row.MissingSinceUtc); Assert.Null(row.TransferredAtUtc);
        }

        // A whole club disappearing is a provider problem: nothing changes.
        clock.Advance(TimeSpan.FromDays(1));
        catalogue.Set([("Glitch fiorentina", "Fiorentina", Role.Forward), ("Glitch extra", "Fiorentina", Role.Forward),
            ("Glitch extra b", "Fiorentina", Role.Forward), ("Glitch extra c", "Fiorentina", Role.Forward), ("Glitch extra d", "Fiorentina", Role.Forward),
            ("Glitch extra e", "Fiorentina", Role.Forward)]);
        var refused = await RunAsync();
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        await using (var database = factory.Database.CreateContext())
        {
            Assert.Equal(7, await database.PlayerSeasons.CountAsync(x => x.SeasonIndex == year && x.IsActive && x.MissingSinceUtc == null));
            Assert.False(await database.Players.AnyAsync(x => x.Name == "Glitch extra"));
        }
    }

}

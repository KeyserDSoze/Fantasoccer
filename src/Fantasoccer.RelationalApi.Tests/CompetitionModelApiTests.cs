using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Auctions;
using Fantasoccer.Application.Core.Caching;
using Fantasoccer.Application.Core.Competitions;
using Fantasoccer.Application.Core.Football;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Application.Core.Market;
using Fantasoccer.Application.Core.Teams;
using Fantasoccer.Caching;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed class CompetitionModelApiTests(WorkspaceApiFactory factory) : IClassFixture<WorkspaceApiFactory>
{
    private sealed record Member(HttpClient Client, AccountIdentityDto Account) : IDisposable { public void Dispose() => Client.Dispose(); }
    private sealed record World(HttpClient Admin, Guid Group, int Year, Guid SerieA, Guid Premier, Guid LeagueSA, Guid LeaguePL,
        Member Mario, Member Luigi, Member Peach, Member Toad, Guid Draghi, Guid Koopa, Guid Leoni, Guid Funghi);
    private static readonly int[] Roles = [0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 1];

    [PostgreSqlFact]
    public async Task A_user_can_own_teams_in_several_baskets_but_never_two_participants_of_one_league_season()
    {
        var world = await WorldAsync(41, peachOwnsFunghi: true);
        var (admin, group, year) = (world.Admin, world.Group, world.Year);
        var details = (await admin.GetFromJsonAsync<GroupDetailsDto>($"/api/workspace/groups/{group}"))!;
        // Mario owns two teams of the same group, in two autonomous baskets.
        Assert.Equal(2, details.Teams.Count(x => x.Owners.Any(o => o.UserId == world.Mario.Account.Id)));
        var serieA = await Read<LeagueParticipantsDto>(await world.Mario.Client.GetAsync($"/api/competitions/{group}/{world.LeagueSA}/{year}/participants"));
        Assert.Equal(new[] { world.Draghi, world.Koopa }.Order(), serieA.Participants.Select(x => x.TeamId).Order());
        Assert.All(serieA.Participants, x => Assert.Equal((int)ParticipationSource.BasketEnrollment, x.Source));

        // Linking both baskets to one league would give Mario two participants.
        await AssertFaultAsync(await admin.PostAsJsonAsync($"/api/workspace/groups/{group}/leagues", new CreateLeagueRequest("Mixed", year, [world.SerieA, world.Premier])),
            HttpStatusCode.Conflict, "league_owner_conflict");
        Assert.DoesNotContain((await admin.GetFromJsonAsync<GroupDetailsDto>($"/api/workspace/groups/{group}"))!.Leagues, x => x.Name == "Mixed");

        // Owner + co-owner through the whole-group editor is refused atomically.
        var graph = (await admin.GetFromJsonAsync<GameGroupDto>($"/api/groups/{group}"))!;
        GameGroupDto With(Guid basket, Guid team, string[] coOwners) => graph with { Baskets = graph.Baskets.Select(b => b.Id != basket ? b : b with
            { Years = b.Years.Select(y => y with { Teams = y.Teams.Select(t => t.TeamId == team ? t with { AdditionalOwners = coOwners } : t).ToArray() }).ToArray() }).ToArray() };
        await AssertFaultAsync(await admin.PutAsJsonAsync($"/api/groups/{group}", With(world.SerieA, world.Koopa, [world.Mario.Account.Email])), HttpStatusCode.Conflict, "league_owner_conflict");
        await using (var database = factory.Database.CreateContext())
            Assert.False(await database.TeamOwnerships.AnyAsync(x => x.TeamId == world.Koopa && x.UserId == world.Mario.Account.Id));
        // Co-owning a team of another league is allowed.
        (await admin.PutAsJsonAsync($"/api/groups/{group}", With(world.Premier, world.Funghi, [world.Luigi.Account.Email]))).EnsureSuccessStatusCode();

        // Raw SQL cannot bypass the rule (co-owner + owner of two SA participants).
        await using (var database = factory.Database.CreateContext())
        {
            database.TeamOwnerships.Add(new() { GroupId = group, TeamId = world.Draghi, BasketId = world.SerieA, SeasonIndex = year, UserId = world.Luigi.Account.Id });
            var fault = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
            Assert.True(DatabaseFaults.TryTranslate(fault, out var translated)); Assert.Equal("league_owner_conflict", translated.Code);
        }
        // Concurrent writers: exactly one of two conflicting co-ownerships commits.
        async Task<bool> TryCoOwnAsync(Guid team)
        {
            await using var database = factory.Database.CreateContext();
            await using var transaction = await database.Database.BeginTransactionAsync();
            database.TeamOwnerships.Add(new() { GroupId = group, TeamId = team, BasketId = world.SerieA, SeasonIndex = year, UserId = world.Toad.Account.Id });
            try { await database.SaveChangesAsync(); await Task.Delay(300); await transaction.CommitAsync(); return true; }
            catch (DbUpdateException) { return false; }
        }
        var results = await Task.WhenAll(TryCoOwnAsync(world.Draghi), TryCoOwnAsync(world.Koopa));
        Assert.Single(results, x => x);
        await using var final = factory.Database.CreateContext();
        Assert.Equal(1, await final.LeagueParticipantOwners.CountAsync(x => x.GroupId == group && x.LeagueId == world.LeagueSA && x.UserId == world.Toad.Account.Id));
        Assert.Equal(1, await final.LeagueParticipantOwners.CountAsync(x => x.GroupId == group && x.LeagueId == world.LeaguePL && x.UserId == world.Peach.Account.Id));
    }

    [PostgreSqlFact]
    public async Task Derived_league_qualifies_teams_from_two_source_leagues_as_carbon_copies_without_own_economy_and_without_cycles()
    {
        var world = await WorldAsync(42, peachOwnsFunghi: true);
        var (admin, group, year) = (world.Admin, world.Group, world.Year);
        var path = $"/api/workspace/groups/{group}";
        var champSA = (await Read<BasketCreatedDto>(await admin.PostAsJsonAsync(path + "/baskets", new CreateBasketRequest("Serie A Champions", year, world.SerieA)))).BasketId;
        var champPL = (await Read<BasketCreatedDto>(await admin.PostAsJsonAsync(path + "/baskets", new CreateBasketRequest("Premier Champions", year, world.Premier)))).BasketId;
        // One level only, consistent kinds, same group.
        await AssertFaultAsync(await admin.PostAsJsonAsync(path + "/baskets", new CreateBasketRequest("Chain", year, champSA)), HttpStatusCode.BadRequest, "invalid_competition_structure");
        await AssertFaultAsync(await admin.PostAsJsonAsync(path + "/leagues", new CreateLeagueRequest("Wrong", year, [world.SerieA], 1)), HttpStatusCode.BadRequest, "invalid_competition_structure");
        await AssertFaultAsync(await admin.PostAsJsonAsync(path + "/teams", new CreateTeamRequest(champSA, year, "Own roster", world.Toad.Account.Id, [])), HttpStatusCode.BadRequest, "invalid_competition_structure");
        await AssertFaultAsync(await admin.PostAsJsonAsync(path + "/teams", new CreateTeamRequest(champSA, year, "Wrong origin", Guid.Empty, [], world.Leoni)), HttpStatusCode.BadRequest, "invalid_competition_structure");
        using var stranger = await factory.AccountAsync(globalAdmin: true);
        var other = (await Read<GroupSummaryDto>(await stranger.PostAsJsonAsync("/api/workspace/groups", new CreateGroupRequest("Other group")))).GroupId;
        var otherBasket = (await Read<BasketCreatedDto>(await stranger.PostAsJsonAsync($"/api/workspace/groups/{other}/baskets", new CreateBasketRequest("Other", year)))).BasketId;
        var otherLeague = (await Read<LeagueCreatedDto>(await stranger.PostAsJsonAsync($"/api/workspace/groups/{other}/leagues", new CreateLeagueRequest("Other", year, [otherBasket])))).LeagueId;
        await AssertFaultAsync(await admin.PostAsJsonAsync(path + "/leagues", new CreateLeagueRequest("Cross", year, [champSA], 1, [new(otherLeague, 1)])), HttpStatusCode.BadRequest, "invalid_competition_structure");

        var champions = (await Read<LeagueCreatedDto>(await admin.PostAsJsonAsync(path + "/leagues",
            new CreateLeagueRequest("Champions", year, [champSA, champPL], 1, [new(world.LeagueSA, 1), new(world.LeaguePL, 1)])))).LeagueId;
        var competition = $"/api/competitions/{group}/{champions}/{year}";
        // Linking derived baskets enrols nobody: participants are qualified explicitly.
        Assert.Empty((await Read<LeagueParticipantsDto>(await admin.GetAsync(competition + "/participants"))).Participants);
        await StandingAsync(group, world.LeagueSA, year, (world.Koopa, world.SerieA), (world.Draghi, world.SerieA));
        await StandingAsync(group, world.LeaguePL, year, (world.Leoni, world.Premier), (world.Funghi, world.Premier));
        var preview = await Read<QualificationPreviewDto>(await world.Luigi.Client.GetAsync(competition + "/qualification"));
        Assert.Equal(new[] { world.Koopa, world.Leoni }.Order(), preview.Candidates.Where(x => x.Qualified).Select(x => x.OriginTeamId).Order());
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Luigi.Client.PostAsJsonAsync(competition + "/qualification/confirm",
            new ConfirmQualificationRequest([new(world.LeagueSA, world.Koopa)]))).StatusCode);
        var confirmed = await Read<LeagueParticipantsDto>(await admin.PostAsJsonAsync(competition + "/qualification/confirm",
            new ConfirmQualificationRequest([new(world.LeagueSA, world.Koopa), new(world.LeaguePL, world.Leoni)])));
        Assert.Equal(2, confirmed.Participants.Length);
        Assert.All(confirmed.Participants, x => { Assert.True(x.IsDerived); Assert.Equal((int)ParticipationSource.Qualification, x.Source); Assert.Equal(1, x.QualificationRank); });
        var derivedKoopa = confirmed.Participants.Single(x => x.AuthoritativeTeamId == world.Koopa);
        Assert.Equal(champSA, derivedKoopa.BasketId);
        Assert.Equal(world.Luigi.Account.Email, derivedKoopa.Owner);
        // Mario already controls the derived Leoni: Draghi cannot join the same league.
        await AssertFaultAsync(await admin.PostAsJsonAsync(competition + "/qualification/confirm",
            new ConfirmQualificationRequest([new(world.LeagueSA, world.Draghi)])), HttpStatusCode.Conflict, "league_owner_conflict");
        // A team that did not play the declared source league cannot be qualified from it.
        await using (var database = factory.Database.CreateContext())
        {
            var draghiCopy = await DerivedTeamAsync(database, group, champSA, year, world.Draghi);
            database.LeagueParticipations.Add(new() { GroupId = group, LeagueId = champions, SeasonIndex = year, TeamId = draghiCopy, BasketId = champSA,
                Source = ParticipationSource.Qualification, SourceLeagueId = world.LeaguePL, AdmittedAtUtc = DateTimeOffset.UtcNow });
            var refused = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
            Assert.True(DatabaseFaults.TryTranslate(refused, out var translated)); Assert.Equal("qualification_without_source_participation", translated.Code);
        }
        // Stable participations: new source standings move nobody.
        await StandingAsync(group, world.LeagueSA, year, (world.Draghi, world.SerieA), (world.Koopa, world.SerieA));
        Assert.Equal(confirmed.Participants.Select(x => x.TeamId).Order(),
            (await Read<LeagueParticipantsDto>(await admin.GetAsync(competition + "/participants"))).Participants.Select(x => x.TeamId).Order());

        // Carbon copy: the derived team always resolves the origin's current roster.
        var players = await RosterAsync(group, world.Koopa, year, 2);
        var rosterPath = $"/api/teams/roster?groupId={group}&basketId={champSA}&year={year}&owner={Uri.EscapeDataString(world.Luigi.Account.Email)}&kind=0&day=0&teamId={derivedKoopa.TeamId}";
        Assert.Equal(2, (await world.Luigi.Client.GetFromJsonAsync<TeamRosterDto>(rosterPath))!.Players.Count(x => x.Status == 0));
        await using (var database = factory.Database.CreateContext())
        {
            var roster = await database.RosterSnapshots.Include(x => x.Players).SingleAsync(x => x.TeamId == world.Koopa && x.Kind == RosterKind.Season);
            var bought = new FootballPlayer { Name = "Bought " + Guid.NewGuid().ToString("N") };
            database.PlayerSeasons.Add(new() { Player = bought, SeasonIndex = year, NormalizedName = KeyUtils.GetPlayerKey(bought.Name), ClubId = "cmp", Position = PlayerPosition.Forward });
            roster.Players.Add(new() { Ordinal = 50, Player = bought, ClubId = "cmp", PlayerRole = 3, Status = 0, Position = 11, IsActive = true, Price = 9 });
            roster.Players.Single(x => x.PlayerId == players[0]).Status = 3; roster.Version++;
            await database.SaveChangesAsync();
        }
        var copy = (await world.Luigi.Client.GetFromJsonAsync<TeamRosterDto>(rosterPath))!;
        Assert.Equal(2, copy.Players.Count(x => x.Status == 0));
        Assert.Contains(copy.Players, x => x.Status == 3 && x.PlayerId == players[0]);

        // No own owners, rosters, market or auction for derived teams/leagues.
        await using (var database = factory.Database.CreateContext())
        {
            database.TeamOwnerships.Add(new() { GroupId = group, TeamId = derivedKoopa.TeamId, BasketId = champSA, SeasonIndex = year, UserId = world.Toad.Account.Id });
            var refused = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
            Assert.True(DatabaseFaults.TryTranslate(refused, out var translated)); Assert.Equal("derived_team_ownership", translated.Code);
        }
        await using (var database = factory.Database.CreateContext())
        {
            database.RosterSnapshots.Add(new() { GroupId = group, BasketId = champSA, SeasonIndex = year, TeamOriginYear = year, TeamId = derivedKoopa.TeamId,
                Kind = RosterKind.Season, OwnerUserId = world.Luigi.Account.Id, Name = "Forged" });
            var refused = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
            Assert.True(DatabaseFaults.TryTranslate(refused, out var translated)); Assert.Equal("derived_team_roster", translated.Code);
        }
        await AssertFaultAsync(await world.Luigi.Client.PostAsJsonAsync(competition + "/market", new CreateMarketRequest(derivedKoopa.TeamId,
            confirmed.Participants.Single(x => x.TeamId != derivedKoopa.TeamId).TeamId, [], [], 0, 0, 1, 1)), HttpStatusCode.Conflict, "derived_league_economy");
        await AssertFaultAsync(await admin.PostAsJsonAsync("/api/auctions", new CreateAuctionRequest(group, champions, year, 0, 0)), HttpStatusCode.Conflict, "derived_league_economy");

        // A derived league may feed another one, but never in a cycle.
        var super = (await Read<LeagueCreatedDto>(await admin.PostAsJsonAsync(path + "/leagues",
            new CreateLeagueRequest("Supercoppa", year, [champSA], 1, [new(champions, 1)])))).LeagueId;
        await using (var database = factory.Database.CreateContext())
        {
            database.LeagueDerivations.Add(new() { GroupId = group, LeagueId = champions, SeasonIndex = year, SourceLeagueId = super, QualifiedTeams = 1 });
            var cycle = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
            Assert.True(DatabaseFaults.TryTranslate(cycle, out var translated)); Assert.Equal("league_derivation_cycle", translated.Code);
        }
        await using (var database = factory.Database.CreateContext())
        {
            // Cross-season derivation: refused by the composite foreign key itself.
            database.Seasons.Add(new() { SeasonIndex = year + 50, StartYear = year + 2061 });
            database.LeagueDerivations.Add(new() { GroupId = group, LeagueId = super, SeasonIndex = year + 50, SourceLeagueId = world.LeagueSA, QualifiedTeams = 1 });
            var crossSeason = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(crossSeason.InnerException).SqlState);
        }
        var structure = (await admin.GetFromJsonAsync<GroupDetailsDto>(path))!;
        Assert.Equal(world.SerieA, structure.Baskets.Single(x => x.BasketId == champSA).OriginBasketId);
        Assert.Equal(1, structure.Leagues.Single(x => x.LeagueId == champions).Mode);
        Assert.Equal(world.Koopa, structure.Teams.Single(x => x.TeamId == derivedKoopa.TeamId).OriginTeamId);
    }

    [PostgreSqlFact]
    public async Task Each_competition_keeps_its_own_lineup_copy_is_validated_and_results_use_the_competition_lineup()
    {
        var world = await WorldAsync(43, peachOwnsFunghi: false);
        var (admin, group, year) = (world.Admin, world.Group, world.Year);
        var path = $"/api/workspace/groups/{group}";
        var champSA = (await Read<BasketCreatedDto>(await admin.PostAsJsonAsync(path + "/baskets", new CreateBasketRequest("SA Cup", year, world.SerieA)))).BasketId;
        var champPL = (await Read<BasketCreatedDto>(await admin.PostAsJsonAsync(path + "/baskets", new CreateBasketRequest("PL Cup", year, world.Premier)))).BasketId;
        var champions = (await Read<LeagueCreatedDto>(await admin.PostAsJsonAsync(path + "/leagues",
            new CreateLeagueRequest("Champions", year, [champSA, champPL], 1, [new(world.LeagueSA, 2), new(world.LeaguePL, 2)])))).LeagueId;
        var confirmed = await Read<LeagueParticipantsDto>(await admin.PostAsJsonAsync($"/api/competitions/{group}/{champions}/{year}/qualification/confirm",
            new ConfirmQualificationRequest([new(world.LeagueSA, world.Koopa), new(world.LeaguePL, world.Funghi)])));
        var copyKoopa = confirmed.Participants.Single(x => x.AuthoritativeTeamId == world.Koopa).TeamId;
        var copyFunghi = confirmed.Participants.Single(x => x.AuthoritativeTeamId == world.Funghi).TeamId;
        var koopaPlayers = await RosterAsync(group, world.Koopa, year, Roles.Length);
        await RosterAsync(group, world.Draghi, year, Roles.Length); await RosterAsync(group, world.Funghi, year, Roles.Length);
        await MatchdayAsync(year, 1, DateTimeOffset.UtcNow.AddDays(1));
        var gameSA = await GameAsync(group, world.LeagueSA, year, (world.Koopa, world.SerieA, world.Luigi.Account.Email), (world.Draghi, world.SerieA, world.Mario.Account.Email));
        var gameCL = await GameAsync(group, champions, year, (copyKoopa, champSA, world.Luigi.Account.Email), (copyFunghi, champPL, world.Toad.Account.Email));
        var luigi = world.Luigi.Client;
        string Game(Guid league, Guid game) => $"/api/competitions/{group}/{league}/{year}/games/{game}";
        async Task<LiveTeamDto> Team(Guid league, Guid game, Guid team, string mode = "formation") =>
            (await Read<GameDto>(await luigi.GetAsync(Game(league, game) + "?editMode=" + mode))).Teams.Single(x => x.TeamId == team);
        FormationPositionRequest[] Positions(LiveTeamDto team, params (int Index, int Position)[] changes)
        {
            var map = team.Players.ToDictionary(x => x.Current.PlayerId, x => x.Current.Position);
            foreach (var (index, position) in changes) map[koopaPlayers[index]] = position;
            return map.Select(x => new FormationPositionRequest(x.Key, x.Value)).ToArray();
        }
        int Position(LiveTeamDto team, int index) => team.Players.Single(x => x.Current.PlayerId == koopaPlayers[index]).Current.Position;

        (await luigi.PutAsJsonAsync("/api/teams/lineup-settings", new SetTeamLineupSettingsRequest(group, year, world.Koopa, true))).EnsureSuccessStatusCode();
        var initial = await Team(world.LeagueSA, gameSA, world.Koopa);
        Assert.True(initial.CanEdit); Assert.Equal(0, initial.Version); Assert.NotNull(initial.SourceVersion);
        // Lineup 1 in Serie A: swap the first defender with the bench defender.
        var saved = await Read<FormationSavedDto>(await luigi.PutAsJsonAsync(Game(world.LeagueSA, gameSA) + "/formation",
            new SaveGameFormationRequest(world.Koopa, Positions(initial, (1, 5), (11, 1)), 0, initial.SourceVersion)));
        Assert.Equal(1, saved.Team.Version);
        // The Champions lineup of the same roster is untouched.
        var champion = await Team(champions, gameCL, copyKoopa);
        Assert.True(champion.CanEdit); Assert.Equal(1, Position(champion, 1)); Assert.Equal(5, Position(champion, 11)); Assert.Equal(world.Koopa, champion.AuthoritativeTeamId);
        var sources = (await luigi.GetFromJsonAsync<LineupCopySourceDto[]>($"/api/competitions/{group}/{champions}/{year}/lineups/copy-sources?day=1&teamId={copyKoopa}"))!;
        var source = Assert.Single(sources); Assert.Equal(world.LeagueSA, source.LeagueId); Assert.True(source.HasLineup);
        // Copy Serie A -> Champions.
        var copied = await Read<FormationSavedDto>(await luigi.PostAsJsonAsync(Game(champions, gameCL) + "/formation/copy",
            new CopyGameFormationRequest(copyKoopa, world.LeagueSA, world.Koopa, 0, champion.SourceVersion)));
        Assert.Equal("Formazione copiata.", copied.Message);
        champion = await Team(champions, gameCL, copyKoopa);
        Assert.Equal(5, Position(champion, 1)); Assert.Equal(1, Position(champion, 11)); Assert.Equal(1, champion.Version);
        // Later changes in Serie A do not propagate.
        var serieA = await Team(world.LeagueSA, gameSA, world.Koopa);
        (await luigi.PutAsJsonAsync(Game(world.LeagueSA, gameSA) + "/formation", new SaveGameFormationRequest(world.Koopa,
            Positions(serieA, (1, 1), (2, 5)), serieA.Version))).EnsureSuccessStatusCode();
        champion = await Team(champions, gameCL, copyKoopa);
        Assert.Equal(5, Position(champion, 1)); Assert.Equal(1, Position(champion, 2));
        // Copy needs the same authoritative roster, the target's permissions and a current version.
        await AssertFaultAsync(await luigi.PostAsJsonAsync(Game(champions, gameCL) + "/formation/copy",
            new CopyGameFormationRequest(copyKoopa, world.LeagueSA, world.Draghi, 1)), HttpStatusCode.Conflict, "lineup_copy_roster_mismatch");
        await AssertFaultAsync(await world.Mario.Client.PostAsJsonAsync(Game(champions, gameCL) + "/formation/copy",
            new CopyGameFormationRequest(copyKoopa, world.LeagueSA, world.Koopa, 1)), HttpStatusCode.Forbidden, "team_access_denied");
        await AssertFaultAsync(await luigi.PostAsJsonAsync(Game(champions, gameCL) + "/formation/copy",
            new CopyGameFormationRequest(copyKoopa, world.LeagueSA, world.Koopa, 0)), HttpStatusCode.Conflict, "roster_changed");
        // An invalid source lineup cannot be copied around the validations.
        await using (var database = factory.Database.CreateContext())
        {
            var lineup = await database.LeagueLineups.SingleAsync(x => x.LeagueId == world.LeagueSA && x.TeamId == world.Koopa);
            lineup.Players.Single(x => x.PlayerId == koopaPlayers[9]).Position = 11; await database.SaveChangesAsync();
        }
        await AssertFaultAsync(await luigi.PostAsJsonAsync(Game(champions, gameCL) + "/formation/copy",
            new CopyGameFormationRequest(copyKoopa, world.LeagueSA, world.Koopa, 1)), HttpStatusCode.BadRequest, "invalid_formation");
        await using (var database = factory.Database.CreateContext())
        {
            var lineup = await database.LeagueLineups.SingleAsync(x => x.LeagueId == world.LeagueSA && x.TeamId == world.Koopa);
            lineup.Players.Single(x => x.PlayerId == koopaPlayers[9]).Position = 3; await database.SaveChangesAsync();
        }

        // SuperAdmin AIgoritmo works on the selected team, not on the admin's own.
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync($"/api/competitions/{group}/{world.LeagueSA}/{year}/optimal-formation?day=1&teamId={world.Koopa}&asAdmin=false")).StatusCode);
        var optimal = await Read<OptimalGameFormationDto>(await admin.GetAsync($"/api/competitions/{group}/{world.LeagueSA}/{year}/optimal-formation?day=1&teamId={world.Koopa}&asAdmin=true"));
        Assert.All(optimal.Players, x => Assert.Contains(x.PlayerId, koopaPlayers));

        // Live: league rules differ (1 change in Serie A, none in the Champions).
        await using (var database = factory.Database.CreateContext())
        {
            (await database.LeagueRules.SingleAsync(x => x.GroupId == group && x.LeagueId == world.LeagueSA && x.SeasonIndex == year)).LiveFormationChanges = 1;
            (await database.FootballMatches.SingleAsync(x => x.SeasonIndex == year && x.SerieADay == 1)).KickoffUtc = DateTimeOffset.UtcNow.AddMinutes(-30);
            await database.SaveChangesAsync();
        }
        serieA = await Team(world.LeagueSA, gameSA, world.Koopa, "live");
        await AssertFaultAsync(await luigi.PutAsJsonAsync(Game(world.LeagueSA, gameSA) + "/formation", new SaveGameFormationRequest(world.Koopa,
            Positions(serieA, (2, 1), (3, 5)), serieA.Version)), HttpStatusCode.Conflict, "formation_deadline_passed");
        (await luigi.PutAsJsonAsync(Game(world.LeagueSA, gameSA) + "/formation", new SaveGameFormationRequest(world.Koopa,
            Positions(serieA, (2, 1), (3, 5)), serieA.Version, Live: true))).EnsureSuccessStatusCode();
        serieA = await Team(world.LeagueSA, gameSA, world.Koopa, "live");
        // Only the live swap is traced; the pre-match saves are not.
        Assert.Equal(1, serieA.FormationChangesUsed); Assert.Single(serieA.FormationChangeHistory);
        await AssertFaultAsync(await luigi.PutAsJsonAsync(Game(world.LeagueSA, gameSA) + "/formation", new SaveGameFormationRequest(world.Koopa,
            Positions(serieA, (3, 1), (4, 5)), serieA.Version, Live: true)), HttpStatusCode.Conflict, "live_changes_exhausted");
        champion = await Team(champions, gameCL, copyKoopa, "live");
        await AssertFaultAsync(await luigi.PutAsJsonAsync(Game(champions, gameCL) + "/formation", new SaveGameFormationRequest(copyKoopa,
            Positions(champion, (2, 5), (11, 1)), champion.Version, Live: true)), HttpStatusCode.Conflict, "live_changes_exhausted");

        // A sale after saving is reported on the lineup, never silently corrected.
        await using (var database = factory.Database.CreateContext())
        {
            var day = await database.RosterSnapshots.Include(x => x.Players).SingleAsync(x => x.TeamId == world.Koopa && x.Kind == RosterKind.Matchday && x.Day == 1);
            day.Players.Single(x => x.PlayerId == koopaPlayers[10]).Status = 3; day.Version++; await database.SaveChangesAsync();
        }
        serieA = await Team(world.LeagueSA, gameSA, world.Koopa, "view");
        Assert.Contains(koopaPlayers[10], serieA.UnavailablePlayerIds);
        await using (var database = factory.Database.CreateContext())
            Assert.Contains(await database.LeagueLineups.Where(x => x.LeagueId == world.LeagueSA && x.TeamId == world.Koopa).SelectMany(x => x.Players).Select(x => x.PlayerId).ToArrayAsync(),
                x => x == koopaPlayers[10]);

        // Results are computed with each competition's own lineup.
        await using (var database = factory.Database.CreateContext())
        {
            var match = await database.FootballMatches.SingleAsync(x => x.SeasonIndex == year && x.SerieADay == 1);
            match.KickoffUtc = DateTimeOffset.UtcNow.AddHours(-5); match.IsFinished = true; match.ActualOfficialVotesReady = true; match.HomeGoals = 1; match.AwayGoals = 0;
            await database.SaveChangesAsync();
        }
        foreach (var league in new[] { world.LeagueSA, champions })
            (await admin.PostAsJsonAsync($"/api/competitions/{group}/{league}/{year}/recalculate", new RecalculateCompetitionRequest(1))).EnsureSuccessStatusCode();
        await using (var database = factory.Database.CreateContext())
        {
            var playedSA = await database.PlayedMatchSnapshots.SingleAsync(x => x.LeagueId == world.LeagueSA && x.MatchId == gameSA);
            var playedCL = await database.PlayedMatchSnapshots.SingleAsync(x => x.LeagueId == champions && x.MatchId == gameCL);
            int Played(PlayedMatchSnapshot snapshot, int index) => snapshot.Players.Single(x => x.IsHome && x.PlayerId == koopaPlayers[index]).Position;
            Assert.Equal(1, Played(playedSA, 2)); Assert.Equal(5, Played(playedSA, 3));
            Assert.Equal(1, Played(playedCL, 2)); Assert.Equal(5, Played(playedCL, 1));
        }
        // Played days are history: the lineup can no longer be edited.
        serieA = await Team(world.LeagueSA, gameSA, world.Koopa);
        await AssertFaultAsync(await luigi.PutAsJsonAsync(Game(world.LeagueSA, gameSA) + "/formation", new SaveGameFormationRequest(world.Koopa,
            Positions(serieA), serieA.Version)), HttpStatusCode.Conflict, "formation_deadline_passed");
    }

    [PostgreSqlFact]
    public async Task A_day_without_a_lineup_proposes_the_previous_one_to_its_owner_until_it_is_saved()
    {
        var world = await WorldAsync(46, peachOwnsFunghi: false);
        var (group, year) = (world.Group, world.Year);
        var koopaPlayers = await RosterAsync(group, world.Koopa, year, Roles.Length);
        await RosterAsync(group, world.Draghi, year, Roles.Length);
        await MatchdayAsync(year, 1, DateTimeOffset.UtcNow.AddDays(1));
        await MatchdayAsync(year, 2, DateTimeOffset.UtcNow.AddDays(8));
        await MatchdayAsync(year, 3, DateTimeOffset.UtcNow.AddDays(15));
        var games = await GameDaysAsync(group, world.LeagueSA, year, (world.Koopa, world.SerieA, world.Luigi.Account.Email), (world.Draghi, world.SerieA, world.Mario.Account.Email), 1, 2, 3);
        // The shared season roster has everybody in the stands, as for a roster never arranged.
        await using (var database = factory.Database.CreateContext())
        {
            var roster = await database.RosterSnapshots.Include(x => x.Players).SingleAsync(x => x.GroupId == group && x.TeamId == world.Koopa && x.Kind == RosterKind.Season);
            foreach (var player in roster.Players.Where(x => x.Status == 0)) player.Position = 11;
            roster.Version++; await database.SaveChangesAsync();
        }
        string Game(Guid game) => $"/api/competitions/{group}/{world.LeagueSA}/{year}/games/{game}";
        async Task<LiveTeamDto> Team(Guid? game = null, HttpClient? client = null, string mode = "formation") =>
            (await Read<GameDto>(await (client ?? world.Luigi.Client).GetAsync(Game(game!.Value) + "?editMode=" + mode))).Teams.Single(x => x.TeamId == world.Koopa);
        var luigi = world.Luigi.Client;

        // Day 1 has nothing to propose yet. Luigi arranges and saves it.
        var first = await Team(game: games[0]);
        Assert.Null(first.ProposedFromDay);
        var arranged = first.Players.ToDictionary(x => x.Current.PlayerId, x => x.Current.Position);
        for (var index = 0; index < Roles.Length; index++) arranged[koopaPlayers[index]] = index == 11 ? 5 : Roles[index];
        arranged[koopaPlayers[1]] = 5; arranged[koopaPlayers[11]] = 1;
        var positions = arranged.Select(x => new FormationPositionRequest(x.Key, x.Value)).ToArray();
        (await luigi.PutAsJsonAsync(Game(games[0]) + "/formation", new SaveGameFormationRequest(world.Koopa, positions, 0, first.SourceVersion))).EnsureSuccessStatusCode();

        // Day 2: the previous lineup is proposed, nothing is stored.
        var second = await Team(game: games[1]);
        Assert.Equal(1, second.ProposedFromDay); Assert.Equal(0, second.Version);
        Assert.Equal(5, second.Players.Single(x => x.Current.PlayerId == koopaPlayers[1]).Current.Position);
        Assert.Equal(1, second.Players.Single(x => x.Current.PlayerId == koopaPlayers[11]).Current.Position);
        await using (var database = factory.Database.CreateContext())
            Assert.False(await database.LeagueLineups.AnyAsync(x => x.LeagueId == world.LeagueSA && x.TeamId == world.Koopa && x.Day == 2));
        // Walks back over days without a formation: day 3 shows the lineup of day 1.
        var third = await Team(game: games[2]);
        Assert.Equal(1, third.ProposedFromDay);
        Assert.Equal(1, third.Players.Single(x => x.Current.PlayerId == koopaPlayers[11]).Current.Position);
        // Neither the opponent nor the read-only view receives it.
        Assert.Null((await Team(game: games[1], client: world.Mario.Client)).ProposedFromDay);
        Assert.Null((await Team(game: games[1], mode: "view")).ProposedFromDay);

        // Confirming it saves the day's own lineup.
        var confirmed = second.Players.Select(x => new FormationPositionRequest(x.Current.PlayerId, x.Current.Position)).ToArray();
        var saved = await Read<FormationSavedDto>(await luigi.PutAsJsonAsync(Game(games[1]) + "/formation", new SaveGameFormationRequest(world.Koopa, confirmed, 0, second.SourceVersion)));
        Assert.Equal(1, saved.Team.Version);
        var after = await Team(game: games[1]);
        Assert.Null(after.ProposedFromDay); Assert.Equal(1, after.Version);
        Assert.Equal(1, after.Players.Single(x => x.Current.PlayerId == koopaPlayers[11]).Current.Position);
    }

    [PostgreSqlFact]
    public async Task Lineups_are_shared_between_the_competitions_of_a_team_unless_the_owner_chooses_separate_ones()
    {
        var world = await WorldAsync(47, peachOwnsFunghi: false);
        var (admin, group, year) = (world.Admin, world.Group, world.Year);
        var path = $"/api/workspace/groups/{group}";
        var champSA = (await Read<BasketCreatedDto>(await admin.PostAsJsonAsync(path + "/baskets", new CreateBasketRequest("SA Cup", year, world.SerieA)))).BasketId;
        var champPL = (await Read<BasketCreatedDto>(await admin.PostAsJsonAsync(path + "/baskets", new CreateBasketRequest("PL Cup", year, world.Premier)))).BasketId;
        var champions = (await Read<LeagueCreatedDto>(await admin.PostAsJsonAsync(path + "/leagues",
            new CreateLeagueRequest("Champions", year, [champSA, champPL], 1, [new(world.LeagueSA, 2), new(world.LeaguePL, 2)])))).LeagueId;
        var confirmed = await Read<LeagueParticipantsDto>(await admin.PostAsJsonAsync($"/api/competitions/{group}/{champions}/{year}/qualification/confirm",
            new ConfirmQualificationRequest([new(world.LeagueSA, world.Koopa), new(world.LeaguePL, world.Funghi)])));
        var copyKoopa = confirmed.Participants.Single(x => x.AuthoritativeTeamId == world.Koopa).TeamId;
        var copyFunghi = confirmed.Participants.Single(x => x.AuthoritativeTeamId == world.Funghi).TeamId;
        var koopaPlayers = await RosterAsync(group, world.Koopa, year, Roles.Length);
        await RosterAsync(group, world.Draghi, year, Roles.Length); await RosterAsync(group, world.Funghi, year, Roles.Length);
        await MatchdayAsync(year, 1, DateTimeOffset.UtcNow.AddDays(1));
        var gameSA = await GameAsync(group, world.LeagueSA, year, (world.Koopa, world.SerieA, world.Luigi.Account.Email), (world.Draghi, world.SerieA, world.Mario.Account.Email));
        var gameCL = await GameAsync(group, champions, year, (copyKoopa, champSA, world.Luigi.Account.Email), (copyFunghi, champPL, world.Toad.Account.Email));
        var luigi = world.Luigi.Client;
        string Game(Guid league, Guid game) => $"/api/competitions/{group}/{league}/{year}/games/{game}";
        async Task<LiveTeamDto> Team(Guid league, Guid game, Guid team) =>
            (await Read<GameDto>(await luigi.GetAsync(Game(league, game) + "?editMode=formation"))).Teams.Single(x => x.TeamId == team);
        FormationPositionRequest[] Swap(LiveTeamDto team, int a, int b)
        {
            var map = team.Players.ToDictionary(x => x.Current.PlayerId, x => x.Current.Position);
            (map[koopaPlayers[a]], map[koopaPlayers[b]]) = (map[koopaPlayers[b]], map[koopaPlayers[a]]);
            return map.Select(x => new FormationPositionRequest(x.Key, x.Value)).ToArray();
        }
        int Position(LiveTeamDto team, int index) => team.Players.Single(x => x.Current.PlayerId == koopaPlayers[index]).Current.Position;

        // Automatic pre-match copy by default; each competition still owns its lineup.
        string Settings() => $"/api/teams/lineup-settings";
        var setting = await Read<TeamLineupSettingsDto>(await luigi.GetAsync(Settings() + $"?groupId={group}&year={year}&teamId={world.Koopa}"));
        Assert.False(setting.SeparateLineups); Assert.Equal(2, setting.Competitions);
        Assert.False((await Team(world.LeagueSA, gameSA, world.Koopa)).SeparateLineups);
        var serieA = await Team(world.LeagueSA, gameSA, world.Koopa);
        var saved = await Read<FormationSavedDto>(await luigi.PutAsJsonAsync(Game(world.LeagueSA, gameSA) + "/formation", new SaveGameFormationRequest(world.Koopa, Swap(serieA, 1, 11), 0, serieA.SourceVersion)));
        Assert.Contains("Copiata in 1 altra competizione", saved.Message);
        serieA = await Team(world.LeagueSA, gameSA, world.Koopa);
        var champion = await Team(champions, gameCL, copyKoopa);
        foreach (var index in Enumerable.Range(0, Roles.Length)) Assert.Equal(Position(serieA, index), Position(champion, index));
        // Any competition is the source: saving in the Champions reaches Serie A.
        (await luigi.PutAsJsonAsync(Game(champions, gameCL) + "/formation", new SaveGameFormationRequest(copyKoopa, Swap(champion, 2, 3), champion.Version))).EnsureSuccessStatusCode();
        serieA = await Team(world.LeagueSA, gameSA, world.Koopa); champion = await Team(champions, gameCL, copyKoopa);
        foreach (var index in Enumerable.Range(0, Roles.Length)) Assert.Equal(Position(champion, index), Position(serieA, index));

        // Only an owner of that exact team can change the setting.
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Mario.Client.PutAsJsonAsync(Settings(), new SetTeamLineupSettingsRequest(group, year, world.Koopa, true))).StatusCode);
        // Separate lineups: nothing propagates, the competition can be switched and copied by hand.
        Assert.True((await Read<TeamLineupSettingsDto>(await luigi.PutAsJsonAsync(Settings(), new SetTeamLineupSettingsRequest(group, year, copyKoopa, true)))).SeparateLineups);
        Assert.True((await Team(world.LeagueSA, gameSA, world.Koopa)).SeparateLineups);
        Assert.True((await Team(champions, gameCL, copyKoopa)).SeparateLineups);
        var sources = (await luigi.GetFromJsonAsync<LineupCopySourceDto[]>($"/api/competitions/{group}/{world.LeagueSA}/{year}/lineups/copy-sources?day=1&teamId={world.Koopa}"))!;
        Assert.Equal(gameCL, Assert.Single(sources).GameId);
        var beforeChampion = Position(champion, 1);
        serieA = await Team(world.LeagueSA, gameSA, world.Koopa);
        var separate = await Read<FormationSavedDto>(await luigi.PutAsJsonAsync(Game(world.LeagueSA, gameSA) + "/formation", new SaveGameFormationRequest(world.Koopa, Swap(serieA, 1, 11), serieA.Version)));
        Assert.DoesNotContain("Copiata", separate.Message);
        Assert.Equal(beforeChampion, Position(await Team(champions, gameCL, copyKoopa), 1));

        // Back to shared: the next save propagates again.
        Assert.False((await Read<TeamLineupSettingsDto>(await luigi.PutAsJsonAsync(Settings(), new SetTeamLineupSettingsRequest(group, year, world.Koopa, false)))).SeparateLineups);
        // Enabling automatic copy is not a retroactive overwrite of divergent formations.
        Assert.Equal(beforeChampion, Position(await Team(champions, gameCL, copyKoopa), 1));
        serieA = await Team(world.LeagueSA, gameSA, world.Koopa);
        var again = await Read<FormationSavedDto>(await luigi.PutAsJsonAsync(Game(world.LeagueSA, gameSA) + "/formation", new SaveGameFormationRequest(world.Koopa, Swap(serieA, 1, 11), serieA.Version)));
        Assert.Contains("Copiata in 1", again.Message);
        await using (var database = factory.Database.CreateContext())
            Assert.Single(await database.TeamLineupSettings.Where(x => x.TeamId == world.Koopa).ToArrayAsync());

        // After kickoff the save is refused as usual, so nothing is propagated.
        await using (var database = factory.Database.CreateContext())
        {
            (await database.FootballMatches.SingleAsync(x => x.SeasonIndex == year && x.SerieADay == 1)).KickoffUtc = DateTimeOffset.UtcNow.AddMinutes(-30);
            await database.SaveChangesAsync();
        }
        serieA = await Team(world.LeagueSA, gameSA, world.Koopa);
        await AssertFaultAsync(await luigi.PutAsJsonAsync(Game(world.LeagueSA, gameSA) + "/formation", new SaveGameFormationRequest(world.Koopa, Swap(serieA, 2, 3), serieA.Version)),
            HttpStatusCode.Conflict, "formation_deadline_passed");
    }

    [PostgreSqlFact]
    public async Task A_day_scored_without_a_lineup_uses_the_previous_one_instead_of_the_penalty()
    {
        var world = await WorldAsync(48, peachOwnsFunghi: false);
        var (admin, group, year) = (world.Admin, world.Group, world.Year);
        var koopaPlayers = await RosterAsync(group, world.Koopa, year, Roles.Length);
        await RosterAsync(group, world.Draghi, year, Roles.Length);
        await MatchdayAsync(year, 1, DateTimeOffset.UtcNow.AddDays(1));
        await MatchdayAsync(year, 2, DateTimeOffset.UtcNow.AddDays(8));
        var games = await GameDaysAsync(group, world.LeagueSA, year, (world.Koopa, world.SerieA, world.Luigi.Account.Email), (world.Draghi, world.SerieA, world.Mario.Account.Email), 1, 2);
        await using (var database = factory.Database.CreateContext())
        {
            var roster = await database.RosterSnapshots.Include(x => x.Players).SingleAsync(x => x.GroupId == group && x.TeamId == world.Koopa && x.Kind == RosterKind.Season);
            foreach (var player in roster.Players.Where(x => x.Status == 0)) player.Position = 11;
            roster.Version++; await database.SaveChangesAsync();
        }
        string Game(Guid game) => $"/api/competitions/{group}/{world.LeagueSA}/{year}/games/{game}";
        var luigi = world.Luigi.Client;
        var first = (await Read<GameDto>(await luigi.GetAsync(Game(games[0]) + "?editMode=formation"))).Teams.Single(x => x.TeamId == world.Koopa);
        var arranged = first.Players.ToDictionary(x => x.Current.PlayerId, x => x.Current.Position);
        for (var index = 0; index < Roles.Length; index++) arranged[koopaPlayers[index]] = index == 11 ? 5 : Roles[index];
        arranged[koopaPlayers[1]] = 5; arranged[koopaPlayers[11]] = 1;
        (await luigi.PutAsJsonAsync(Game(games[0]) + "/formation", new SaveGameFormationRequest(world.Koopa,
            arranged.Select(x => new FormationPositionRequest(x.Key, x.Value)).ToArray(), 0, first.SourceVersion))).EnsureSuccessStatusCode();

        // Day 2 is played without any lineup of its own.
        await using (var database = factory.Database.CreateContext())
        {
            foreach (var match in await database.FootballMatches.Where(x => x.SeasonIndex == year).ToArrayAsync())
            { match.KickoffUtc = DateTimeOffset.UtcNow.AddHours(-5 - match.SerieADay); match.IsFinished = true; match.ActualOfficialVotesReady = true; match.HomeGoals = 1; match.AwayGoals = 0; }
            await database.SaveChangesAsync();
        }
        (await admin.PostAsJsonAsync($"/api/competitions/{group}/{world.LeagueSA}/{year}/recalculate", new RecalculateCompetitionRequest(2))).EnsureSuccessStatusCode();
        await using (var database = factory.Database.CreateContext())
        {
            var played = await database.PlayedMatchSnapshots.SingleAsync(x => x.LeagueId == world.LeagueSA && x.MatchId == games[1]);
            int Played(int index) => played.Players.Single(x => x.IsHome && x.PlayerId == koopaPlayers[index]).Position;
            Assert.Equal(5, Played(1)); Assert.Equal(1, Played(11)); Assert.Equal(Roles[2], Played(2));
        }
    }

    [PostgreSqlFact]
    public async Task Live_projection_is_shared_by_members_rebuilt_only_on_source_changes_isolated_per_group_and_recovered_after_cache_loss()
    {
        var world = await WorldAsync(44, peachOwnsFunghi: true);
        var (admin, group, year) = (world.Admin, world.Group, world.Year);
        var koopaPlayers = await RosterAsync(group, world.Koopa, year, Roles.Length);
        await RosterAsync(group, world.Draghi, year, Roles.Length);
        await MatchdayAsync(year, 1, DateTimeOffset.UtcNow.AddDays(1));
        var game = await GameAsync(group, world.LeagueSA, year, (world.Koopa, world.SerieA, world.Luigi.Account.Email), (world.Draghi, world.SerieA, world.Mario.Account.Email));
        await StandingAsync(group, world.LeagueSA, year, (world.Koopa, world.SerieA), (world.Draghi, world.SerieA));
        var livePath = $"/api/groups/{group}/live?year={year}";
        var first = await Read<LiveGroupDto>(await world.Luigi.Client.GetAsync(livePath));
        var second = await Read<LiveGroupDto>(await world.Mario.Client.GetAsync(livePath));
        Assert.Equal(1, first.SerieADay); Assert.False(first.IsStale);
        Assert.Equal(first.GeneratedAtUtc, second.GeneratedAtUtc); // one shared projection, built once
        var serieA = Assert.Single(first.Leagues, x => x.Id == world.LeagueSA);
        Assert.Single(serieA.Rounds.Values.SelectMany(x => x.Games), x => x.Id == game);
        var coordinator = factory.Services.GetRequiredService<IDistributedCacheCoordinator>();
        var stored = await coordinator.GetEntryAsync(CacheKeys.LiveGroup(group, year, 1));
        using (var json = JsonDocument.Parse(stored!.Value))
        {
            Assert.Equal(group, json.RootElement.GetProperty("payload").GetProperty("groupId").GetGuid());
            Assert.DoesNotContain("\"canEdit\":true", stored.Value.Length > 0 ? System.Text.Encoding.UTF8.GetString(stored.Value) : "");
        }
        // Read-only game views of the live day come from the same projection.
        var view = await Read<GameDto>(await world.Mario.Client.GetAsync($"/api/competitions/{group}/{world.LeagueSA}/{year}/games/{game}"));
        Assert.All(view.Teams, x => Assert.False(x.CanEdit));
        Assert.Equal(first.GeneratedAtUtc, (await Read<LiveGroupDto>(await world.Mario.Client.GetAsync(livePath))).GeneratedAtUtc);

        // Global live votes changed: every group using that day is rebuilt.
        await factory.Services.GetRequiredService<IFootballLiveDataStore>().PublishLiveVotesAsync(DateTimeOffset.UtcNow, new(year, 1,
            [new(koopaPlayers[9], "Scorer", "cmp", "Competition club", 3, true, true, new(3, 7, false, 1, 0, 0, 0, 0, 0, 0, 0, false, true, false, false, false))]), null);
        var afterVotes = await Read<LiveGroupDto>(await world.Mario.Client.GetAsync(livePath));
        Assert.True(afterVotes.GeneratedAtUtc > first.GeneratedAtUtc);
        // A lineup change invalidates it as well.
        var team = (await Read<GameDto>(await world.Luigi.Client.GetAsync($"/api/competitions/{group}/{world.LeagueSA}/{year}/games/{game}?editMode=formation"))).Teams.Single(x => x.TeamId == world.Koopa);
        var positions = team.Players.Select(x => new FormationPositionRequest(x.Current.PlayerId, x.Current.PlayerId == koopaPlayers[1] ? 5 : x.Current.PlayerId == koopaPlayers[11] ? 1 : x.Current.Position)).ToArray();
        (await world.Luigi.Client.PutAsJsonAsync($"/api/competitions/{group}/{world.LeagueSA}/{year}/games/{game}/formation",
            new SaveGameFormationRequest(world.Koopa, positions, 0, team.SourceVersion))).EnsureSuccessStatusCode();
        var afterLineup = await Read<LiveGroupDto>(await world.Luigi.Client.GetAsync(livePath));
        Assert.True(afterLineup.GeneratedAtUtc > afterVotes.GeneratedAtUtc);
        var shared = await Read<GameDto>(await world.Mario.Client.GetAsync($"/api/competitions/{group}/{world.LeagueSA}/{year}/games/{game}"));
        Assert.Equal(5, shared.Teams.Single(x => x.TeamId == world.Koopa).Players.Single(x => x.Current.PlayerId == koopaPlayers[1]).Current.Position);

        // No cross-group access to the projection.
        using var outsider = await factory.AccountAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync(livePath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"/api/competitions/{group}/{world.LeagueSA}/{year}/games/{game}")).StatusCode);
        // Cache loss is not data loss: the projection is rebuilt from PostgreSQL.
        await factory.Services.GetRequiredService<IDistributedCache>().RemoveAsync(CacheKeys.LiveGroup(group, year, 1));
        factory.Services.GetRequiredService<SharedSnapshotCache>().Forget(CacheKeys.LiveGroup(group, year, 1));
        var rebuilt = await Read<LiveGroupDto>(await world.Mario.Client.GetAsync(livePath));
        Assert.True(rebuilt.GeneratedAtUtc > afterLineup.GeneratedAtUtc);
        Assert.Equal(afterLineup.Leagues.Single(x => x.Id == world.LeagueSA).Rank!.Rounds["@"].Select(x => x.TeamId),
            rebuilt.Leagues.Single(x => x.Id == world.LeagueSA).Rank!.Rounds["@"].Select(x => x.TeamId));
        // The projection never writes definitive standings.
        await using var database = factory.Database.CreateContext();
        var standing = await database.FantasyStandingSnapshots.AsSplitQuery().SingleAsync(x => x.GroupId == group && x.LeagueId == world.LeagueSA);
        Assert.Equal(new[] { world.Koopa, world.Draghi }, standing.Stages.Single().Teams.OrderBy(x => x.Ordinal).Select(x => x.TeamId));
        Assert.Equal(0, standing.SerieADay);
    }

    [PostgreSqlFact]
    public async Task Live_projection_is_rebuilt_by_any_official_vote_change_and_not_by_a_snapshot_that_was_only_reconfirmed()
    {
        var world = await WorldAsync(49, peachOwnsFunghi: true);
        var (group, year) = (world.Group, world.Year);
        var koopaPlayers = await RosterAsync(group, world.Koopa, year, Roles.Length);
        await RosterAsync(group, world.Draghi, year, Roles.Length);
        await MatchdayAsync(year, 1, DateTimeOffset.UtcNow.AddDays(1));
        await GameAsync(group, world.LeagueSA, year, (world.Koopa, world.SerieA, world.Luigi.Account.Email), (world.Draghi, world.SerieA, world.Mario.Account.Email));
        await StandingAsync(group, world.LeagueSA, year, (world.Koopa, world.SerieA), (world.Draghi, world.SerieA));
        var livePath = $"/api/groups/{group}/live?year={year}";
        async Task<LiveGroupDto> ReadAsync() => await Read<LiveGroupDto>(await world.Luigi.Client.GetAsync(livePath));
        var first = await ReadAsync();
        Assert.Equal(first.GeneratedAtUtc, (await ReadAsync()).GeneratedAtUtc);
        Assert.False(first.LiveVotesStale); // the matchday has not started: nothing is overdue

        // Two official votes are published.
        await using var database = factory.Database.CreateContext();
        FootballVote Official(string player, float value) => new() { PlayerId = player, SeasonIndex = year, Day = 1, Source = FootballVoteSource.Official, ClubId = "cmp",
            PlayerRole = 1, IsActive = true, IsVisible = true, VoteRole = 1, Value = value, IsFinal = true, HasVote = true };
        database.FootballVotes.AddRange(Official(koopaPlayers[1], 6), Official(koopaPlayers[2], 6));
        await database.SaveChangesAsync();
        var inserted = await ReadAsync();
        Assert.True(inserted.GeneratedAtUtc > first.GeneratedAtUtc);
        Assert.Equal(inserted.GeneratedAtUtc, (await ReadAsync()).GeneratedAtUtc);

        // A correction that moves the points of two players in opposite directions: the sum of the votes is the same.
        var votes = await database.FootballVotes.Where(x => x.SeasonIndex == year && x.Day == 1 && x.Source == FootballVoteSource.Official).ToArrayAsync();
        votes.Single(x => x.PlayerId == koopaPlayers[1]).Value = 7; votes.Single(x => x.PlayerId == koopaPlayers[2]).Value = 5;
        await database.SaveChangesAsync();
        var compensated = await ReadAsync();
        Assert.True(compensated.GeneratedAtUtc > inserted.GeneratedAtUtc);

        // A field nobody summed before: goals conceded.
        votes.Single(x => x.PlayerId == koopaPlayers[1]).SufferedGoals = 2;
        await database.SaveChangesAsync();
        var conceded = await ReadAsync();
        Assert.True(conceded.GeneratedAtUtc > compensated.GeneratedAtUtc);
        Assert.Equal(conceded.GeneratedAtUtc, (await ReadAsync()).GeneratedAtUtc);

        // The same save with nothing changed writes nothing and rebuilds nothing.
        await database.SaveChangesAsync();
        Assert.Equal(conceded.GeneratedAtUtc, (await ReadAsync()).GeneratedAtUtc);

        // Shared provisional votes: only a change of content rebuilds every group, a later confirmation of the same votes does not.
        var liveData = factory.Services.GetRequiredService<IFootballLiveDataStore>();
        var scorer = new CachedLiveVote(koopaPlayers[9], "Scorer", "cmp", "Competition club", 3, true, true, new(3, 7, false, 1, 0, 0, 0, 0, 0, 0, 0, false, true, false, false, false));
        await liveData.PublishLiveVotesAsync(DateTimeOffset.UtcNow, new(year, 1, [scorer]), null);
        var withLive = await ReadAsync();
        Assert.True(withLive.GeneratedAtUtc > conceded.GeneratedAtUtc);
        Assert.Equal(CachePublishOutcome.Unchanged, await liveData.PublishLiveVotesAsync(DateTimeOffset.UtcNow.AddMinutes(1), new(year, 1, [scorer]), null));
        Assert.Equal(CachePublishOutcome.Published, await liveData.PublishLiveVotesAsync(DateTimeOffset.UtcNow.AddMinutes(6), new(year, 1, [scorer]), null));
        Assert.Equal(withLive.GeneratedAtUtc, (await ReadAsync()).GeneratedAtUtc);
        await liveData.PublishLiveVotesAsync(DateTimeOffset.UtcNow.AddMinutes(7), new(year, 1, [scorer with { Vote = scorer.Vote with { Goals = 2 } }]), null);
        Assert.True((await ReadAsync()).GeneratedAtUtc > withLive.GeneratedAtUtc);
    }

    [PostgreSqlFact]
    public async Task Live_reports_provisional_votes_that_are_overdue_only_while_the_matchday_is_being_played()
    {
        var world = await WorldAsync(50, peachOwnsFunghi: true);
        var (group, year) = (world.Group, world.Year);
        var koopaPlayers = await RosterAsync(group, world.Koopa, year, Roles.Length);
        await RosterAsync(group, world.Draghi, year, Roles.Length);
        await MatchdayAsync(year, 1, DateTimeOffset.UtcNow.AddMinutes(-40));
        await GameAsync(group, world.LeagueSA, year, (world.Koopa, world.SerieA, world.Luigi.Account.Email), (world.Draghi, world.SerieA, world.Mario.Account.Email));
        await StandingAsync(group, world.LeagueSA, year, (world.Koopa, world.SerieA), (world.Draghi, world.SerieA));
        var livePath = $"/api/groups/{group}/live?year={year}";
        async Task<LiveGroupDto> ReadAsync() => await Read<LiveGroupDto>(await world.Luigi.Client.GetAsync(livePath));

        // Forty minutes into the matchday and the live job never published anything.
        var none = await ReadAsync();
        Assert.Equal(1, none.SerieADay); Assert.True(none.LiveVotesStale); Assert.Null(none.LiveVotesUpdatedAtUtc);

        var liveData = factory.Services.GetRequiredService<IFootballLiveDataStore>();
        CachedLiveVote Vote(float value) => new(koopaPlayers[9], "Scorer", "cmp", "Competition club", 3, true, true, new(3, value, false, 0, 0, 0, 0, 0, 0, 0, 0, false, true, false, false, false));
        var late = DateTimeOffset.UtcNow.AddMinutes(-15);
        await liveData.PublishLiveVotesAsync(late, new(year, 1, [Vote(6)]), null);
        var overdue = await ReadAsync();
        Assert.True(overdue.LiveVotesStale); Assert.Equal(late, overdue.LiveVotesUpdatedAtUtc);

        var now = DateTimeOffset.UtcNow;
        await liveData.PublishLiveVotesAsync(now, new(year, 1, [Vote(7)]), null);
        var current = await ReadAsync();
        Assert.False(current.LiveVotesStale); Assert.Equal(now, current.LiveVotesUpdatedAtUtc);

        // The same snapshot read through the football API carries its own age.
        using var reader = await factory.AccountAsync();
        var votes = await Read<PlayersDto<VotedPlayerDto>>(await reader.GetAsync($"/api/football/{year}/days/1/votes/live"));
        Assert.Equal(now, votes.UpdatedAtUtc); Assert.False(votes.IsStale);
    }

    [PostgreSqlFact]
    public async Task Whole_group_editor_round_trips_derived_baskets_teams_and_sources_and_keeps_kinds_immutable()
    {
        var world = await WorldAsync(45, peachOwnsFunghi: true);
        var (admin, group, year) = (world.Admin, world.Group, world.Year);
        var path = $"/api/groups/{group}";
        var graph = (await admin.GetFromJsonAsync<GameGroupDto>(path))!;
        // The frontend editor adds a derived basket linked to an origin team, then PUTs the whole graph.
        var derived = Guid.NewGuid();
        var saved = await Read<GameGroupDto>(await admin.PutAsJsonAsync(path, graph with { Baskets = [.. graph.Baskets,
            new GameBasketDto(derived, "Serie A Cup", [new GameBasketYearDto(year, [new GameAnnualTeamDto("Koopa Cup", "", [], OriginTeamId: world.Koopa)])], 1, world.SerieA)] }));
        var basket = saved.Baskets.Single(x => x.Id == derived);
        Assert.Equal((1, world.SerieA), (basket.Kind, basket.OriginBasketId));
        var copy = Assert.Single(basket.Years.Single().Teams);
        Assert.Equal(world.Koopa, copy.OriginTeamId);
        Assert.Equal(world.Luigi.Account.Email, copy.Owner); // owners always projected from the origin
        // A derived league with its source declared through the same editor: nobody is enrolled.
        var cup = Guid.NewGuid();
        var annual = saved.Leagues.Single(x => x.Id == world.LeagueSA).Years.Single() with { Sources = [new(world.LeagueSA, 2)], ParticipantTeamIds = null };
        saved = await Read<GameGroupDto>(await admin.PutAsJsonAsync(path, saved with { Leagues = [.. saved.Leagues, new GameLeagueDto(cup, "Cup", false, 1, [annual], [derived], 1)] }));
        var league = saved.Leagues.Single(x => x.Id == cup);
        Assert.Equal(1, league.Mode);
        Assert.Equal(world.LeagueSA, Assert.Single(league.Years.Single().Sources!).SourceLeagueId);
        Assert.Empty(league.Years.Single().ParticipantTeamIds!);
        // Saving the projection back unchanged is a no-op round trip.
        saved = await Read<GameGroupDto>(await admin.PutAsJsonAsync(path, saved));
        Assert.Equal(world.Koopa, saved.Baskets.Single(x => x.Id == derived).Years.Single().Teams.Single().OriginTeamId);
        // Kind, origin and mode never change after creation; an autonomous league cannot use a derived basket.
        await AssertFaultAsync(await admin.PutAsJsonAsync(path, saved with { Baskets = saved.Baskets.Select(x => x.Id == derived ? x with { Kind = 0, OriginBasketId = null } : x).ToArray() }),
            HttpStatusCode.BadRequest, "invalid_group");
        await AssertFaultAsync(await admin.PutAsJsonAsync(path, saved with { Leagues = saved.Leagues.Select(x => x.Id == cup ? x with { Mode = 0 } : x).ToArray() }),
            HttpStatusCode.BadRequest, "invalid_group");
        await AssertFaultAsync(await admin.PutAsJsonAsync(path, saved with { Leagues = saved.Leagues.Select(x => x.Id == world.LeagueSA ? x with { BasketsId = [world.SerieA, derived] } : x).ToArray() }),
            HttpStatusCode.BadRequest, "invalid_competition_structure");
        // Derived teams never get owners from the editor.
        var forged = saved with { Baskets = saved.Baskets.Select(x => x.Id != derived ? x : x with { Years = x.Years.Select(y => y with
            { Teams = y.Teams.Select(t => t with { AdditionalOwners = [world.Toad.Account.Email] }).ToArray() }).ToArray() }).ToArray() };
        (await admin.PutAsJsonAsync(path, forged)).EnsureSuccessStatusCode();
        await using var database = factory.Database.CreateContext();
        var derivedTeam = await database.TeamDerivations.Where(x => x.GroupId == group && x.OriginTeamId == world.Koopa).Select(x => x.TeamId).SingleAsync();
        Assert.False(await database.TeamOwnerships.AnyAsync(x => x.TeamId == derivedTeam));
    }

    private async Task<World> WorldAsync(int year, bool peachOwnsFunghi)
    {
        var admin = await factory.AccountAsync(globalAdmin: true);
        var group = (await Read<GroupSummaryDto>(await admin.PostAsJsonAsync("/api/workspace/groups", new CreateGroupRequest("Amici " + year)))).GroupId;
        async Task<Member> MemberAsync(string name)
        {
            var client = await factory.AccountAsync();
            var account = (await client.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
            (await admin.PutAsJsonAsync($"/api/workspace/groups/{group}/members", new SetGroupMemberRequest(account.Email, name, 2))).EnsureSuccessStatusCode();
            return new(client, account);
        }
        var (mario, luigi, peach, toad) = (await MemberAsync("Mario"), await MemberAsync("Luigi"), await MemberAsync("Peach"), await MemberAsync("Toad"));
        var path = $"/api/workspace/groups/{group}";
        var serieA = (await Read<BasketCreatedDto>(await admin.PostAsJsonAsync(path + "/baskets", new CreateBasketRequest("Serie A", year)))).BasketId;
        var premier = (await Read<BasketCreatedDto>(await admin.PostAsJsonAsync(path + "/baskets", new CreateBasketRequest("Premier", year)))).BasketId;
        var leagueSA = (await Read<LeagueCreatedDto>(await admin.PostAsJsonAsync(path + "/leagues", new CreateLeagueRequest("Serie A", year, [serieA])))).LeagueId;
        var leaguePL = (await Read<LeagueCreatedDto>(await admin.PostAsJsonAsync(path + "/leagues", new CreateLeagueRequest("Premier", year, [premier])))).LeagueId;
        async Task<Guid> TeamAsync(Guid basket, string name, Guid owner) =>
            (await Read<TeamCreatedDto>(await admin.PostAsJsonAsync(path + "/teams", new CreateTeamRequest(basket, year, name, owner, [])))).TeamId;
        var draghi = await TeamAsync(serieA, "Draghi FC", mario.Account.Id);
        var koopa = await TeamAsync(serieA, "Koopa FC", luigi.Account.Id);
        var leoni = await TeamAsync(premier, "Leoni FC", mario.Account.Id);
        var funghi = await TeamAsync(premier, "Funghi FC", (peachOwnsFunghi ? peach : toad).Account.Id);
        return new(admin, group, year, serieA, premier, leagueSA, leaguePL, mario, luigi, peach, toad, draghi, koopa, leoni, funghi);
    }

    private async Task<string[]> RosterAsync(Guid group, Guid team, int year, int count)
    {
        await using var database = factory.Database.CreateContext();
        if (!await database.Clubs.AnyAsync(x => x.Id == "cmp"))
        {
            database.Clubs.Add(new FootballClub { Name = "Competition club", Abbreviation = "cmp" });
            database.Clubs.Add(new FootballClub { Name = "Competition rival", Abbreviation = "cmr" });
            await database.SaveChangesAsync();
        }
        var roster = await database.RosterSnapshots.Include(x => x.Players).SingleAsync(x => x.GroupId == group && x.TeamId == team && x.Kind == RosterKind.Season);
        var ids = new List<string>();
        for (var index = 0; index < count; index++)
        {
            var player = new FootballPlayer { Name = "Lineup player " + Guid.NewGuid().ToString("N") };
            database.PlayerSeasons.Add(new() { Player = player, SeasonIndex = year, NormalizedName = KeyUtils.GetPlayerKey(player.Name), ClubId = "cmp", Position = (PlayerPosition)Roles[index], IsActive = true });
            roster.Players.Add(new() { Ordinal = index, Player = player, ClubId = "cmp", PlayerRole = Roles[index], IsActive = true, IsVisible = true,
                Price = 10 + index, Status = 0, Position = index == 11 ? 5 : Roles[index] });
            ids.Add(player.Id);
        }
        roster.Version++;
        await database.SaveChangesAsync();
        return ids.ToArray();
    }

    private async Task MatchdayAsync(int year, int day, DateTimeOffset kickoff)
    {
        await using var database = factory.Database.CreateContext();
        database.FootballMatchdays.Add(new() { SeasonIndex = year, SerieADay = day, Matches =
            [new() { SeasonIndex = year, SerieADay = day, Ordinal = 0, HomeClubId = "cmp", AwayClubId = "cmr", KickoffUtc = kickoff }] });
        await database.SaveChangesAsync();
    }

    private async Task<Guid> GameAsync(Guid group, Guid league, int year, (Guid Team, Guid Basket, string Owner) home, (Guid Team, Guid Basket, string Owner) away)
    {
        await using var database = factory.Database.CreateContext();
        var id = Guid.NewGuid();
        database.FantasyCalendars.Add(new() { GroupId = group, LeagueId = league, SeasonIndex = year, Stages =
            [new() { Stage = "@", GroupId = group, LeagueId = league, SeasonIndex = year, Days =
                [new() { Stage = "@", GroupId = group, LeagueId = league, SeasonIndex = year, Ordinal = 0, FantasyTurn = 5, SerieADay = 1, Games =
                    [new() { GroupId = group, LeagueId = league, SeasonIndex = year, Id = id, Stage = "@", DayOrdinal = 0, HomeTeamId = home.Team, HomeBasketId = home.Basket,
                        AwayTeamId = away.Team, AwayBasketId = away.Basket, HomeName = "Home", AwayName = "Away", HomeOwner = home.Owner, AwayOwner = away.Owner }] }] }] });
        await database.SaveChangesAsync();
        return id;
    }

    private async Task<Guid[]> GameDaysAsync(Guid group, Guid league, int year, (Guid Team, Guid Basket, string Owner) home, (Guid Team, Guid Basket, string Owner) away, params int[] days)
    {
        await using var database = factory.Database.CreateContext();
        var ids = days.Select(_ => Guid.NewGuid()).ToArray();
        database.FantasyCalendars.Add(new() { GroupId = group, LeagueId = league, SeasonIndex = year, Stages =
            [new() { Stage = "@", GroupId = group, LeagueId = league, SeasonIndex = year, Days = days.Select((day, index) => new FantasyMatchday
                { Stage = "@", GroupId = group, LeagueId = league, SeasonIndex = year, Ordinal = index, FantasyTurn = 5 + index, SerieADay = day, Games =
                    [new() { GroupId = group, LeagueId = league, SeasonIndex = year, Id = ids[index], Stage = "@", DayOrdinal = index, HomeTeamId = home.Team, HomeBasketId = home.Basket,
                        AwayTeamId = away.Team, AwayBasketId = away.Basket, HomeName = "Home", AwayName = "Away", HomeOwner = home.Owner, AwayOwner = away.Owner }] }).ToList() }] });
        await database.SaveChangesAsync();
        return ids;
    }

    private async Task StandingAsync(Guid group, Guid league, int year, params (Guid Team, Guid Basket)[] order)
    {
        await using var database = factory.Database.CreateContext();
        await database.FantasyStandingSnapshots.Where(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year).ExecuteDeleteAsync();
        database.FantasyStandingSnapshots.Add(new() { GroupId = group, LeagueId = league, SeasonIndex = year, Stages = [new() { Stage = "@", Teams =
            order.Select((x, index) => new FantasyStandingRow { Ordinal = index, TeamId = x.Team, BasketId = x.Basket, Name = "Team " + index, Owner = "owner",
                Points = 10 - index }).ToList() }] });
        await database.SaveChangesAsync();
    }

    private static async Task<Guid> DerivedTeamAsync(Fantasoccer.Infrastructure.PostgreSql.FantasoccerDbContext database, Guid group, Guid basket, int year, Guid origin)
    {
        var team = new FantasyTeamSeason { Id = Guid.NewGuid(), GroupId = group, BasketId = basket, SeasonIndex = year, Name = "Copy " + origin.ToString("N")[..6], NormalizedName = "copy " + origin.ToString("N")[..6] };
        database.TeamSeasons.Add(team);
        database.TeamDerivations.Add(new() { GroupId = group, BasketId = basket, SeasonIndex = year, TeamId = team.Id, OriginBasketId = (await database.TeamSeasons.SingleAsync(x => x.Id == origin)).BasketId,
            OriginTeamId = origin, CreatedAtUtc = DateTimeOffset.UtcNow });
        await database.SaveChangesAsync();
        return team.Id;
    }

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task AssertFaultAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"Expected {status} {code}, got {(int)response.StatusCode}: {body}");
        Assert.Contains(code, body);
    }
}

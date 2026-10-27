using System.Text.Json;
using Fantasoccer.DataMigration;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.StorageMigration;
using Microsoft.EntityFrameworkCore;
using RepositoryFramework;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed partial class CoreMigrationTests
{
    [CoreFact]
    public async Task Minimal_317_player_feeds_preserve_all_votes_flags_and_baselines_with_NULL_clubs_without_fake_clubs_or_memberships()
    {
        var players = Enumerable.Range(0, 317).Select(index => new VotedRealPlayer
        {
            Name = "Minimal keeper " + new string([(char)('a' + index / 676), (char)('a' + index / 26 % 26), (char)('a' + index % 26)]),
            RealTeam = null!, Role = Role.GoalKeeper, IsActive = false, Visible = true,
            Vote = new() { Role = Role.GoalKeeper, Value = 6, HasVote = true, IsFinal = false, Goal = index % 2, Assist = index % 3 }
        }).ToList();
        var originals = new List<(Azure.Storage.Blobs.BlobClient Blob, string Json, Azure.ETag ETag)>();
        foreach (var source in new[] { "live", "notification", "official", "pushnotificationsent" })
        {
            var container = await ExtraContainerAsync(source); var blob = container.GetBlobClient("minimal-source");
            object value = source == "pushnotificationsent" ? new PushNotificationSent { MatchEventVotes = new() { Players = players }, MatchEventSequence = 3 }
                : new VotedRealPlayerWrapper { Players = players };
            var key = new YearDayKey { Year = 12, Day = 10 };
            var json = source == "live" ? JsonSerializer.Serialize(new { Key = key, Value = value }) : JsonSerializer.Serialize(new { k = key, v = value });
            await blob.UploadAsync(BinaryData.FromString(json)); originals.Add((blob, json, (await blob.GetPropertiesAsync()).Value.ETag));
        }
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await using var database = fixture.CreateContext();
        var report = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(317, await database.Players.CountAsync()); Assert.Equal(317, await database.PlayerSeasons.CountAsync());
        Assert.Equal(951, await database.FootballVotes.CountAsync()); Assert.Empty(await database.Clubs.ToArrayAsync()); Assert.Empty(await database.ClubSeasons.ToArrayAsync());
        Assert.All(await database.PlayerSeasons.ToArrayAsync(), x => Assert.Null(x.ClubId));
        foreach (var source in Enum.GetValues<FootballVoteSource>())
        {
            var votes = await database.FootballVotes.Where(x => x.Source == source).OrderBy(x => x.PlayerId).ToArrayAsync();
            Assert.Equal(317, votes.Length);
            foreach (var original in players)
            {
                var saved = votes.Single(x => x.PlayerId == original.Key);
                Assert.Null(saved.ClubId); Assert.False(saved.IsActive); Assert.True(saved.IsVisible); Assert.True(saved.HasVote);
                Assert.Equal(6, saved.Value); Assert.False(saved.IsFinal); Assert.Equal(original.Vote.Goal, saved.Goals); Assert.Equal(original.Vote.Assist, saved.Assists);
                Assert.Equal(0, saved.PlayerRole); Assert.Equal(0, saved.VoteRole);
            }
        }
        var checkpoint = await database.PushDayCheckpoints.AsNoTracking().AsSplitQuery().SingleAsync();
        Assert.Equal(3, checkpoint.MatchEventSequence); Assert.Equal(317, checkpoint.Votes.Count);
        Assert.All(checkpoint.Votes, x => { Assert.Null(x.ClubId); Assert.True(x.HasVote); Assert.Equal(6, x.Value); Assert.False(x.IsActive); });
        Assert.Empty(await database.PushDeliveries.ToArrayAsync());
        Assert.Equal(4, report.MissingVoteClubSnapshots!.Length); Assert.All(report.MissingVoteClubSnapshots, x => Assert.Equal(317, x.Records));
        Assert.Contains(report.MissingVoteClubSnapshots, x => x.Container == "live" && x.FirstJsonPath == "$.Value.p[0].t" && x.SeasonIndex == 12 && x.Day == 10);
        Assert.Equal(317, report.UnresolvedAnnualPlayerClubs); Assert.False(report.FullMigrationComplete);
        Assert.DoesNotContain("Minimal keeper", JsonSerializer.Serialize(report));
        var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(repeated.Documents, repeated.SkippedDocuments); Assert.Equal(951, await database.FootballVotes.CountAsync());
        foreach (var (blob, json, etag) in originals)
        {
            Assert.Equal(etag, (await blob.GetPropertiesAsync()).Value.ETag);
            Assert.Equal(json, (await blob.DownloadContentAsync()).Value.Content.ToString());
        }
    }

    [CoreFact]
    public async Task Same_year_season_rosters_can_complete_missing_annual_club_but_never_fill_the_historical_vote_club()
    {
        var votes = await ExtraContainerAsync("live");
        await votes.GetBlobClient("source").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<VotedRealPlayerWrapper, YearDayKey>.Default(new()
            { Players = [new() { Name = "Roster linked player", RealTeam = null!, Role = Role.Forward, IsActive = false, Vote = new() { Value = 6, HasVote = true } }] },
            new() { Year = 15, Day = 10 }))));
        var teams = await ExtraContainerAsync("team");
        foreach (var group in new[] { "first", "second" })
            await teams.GetBlobClient(group).UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<Team, TeamKey>.Default(new()
                { Name = "Historical roster", Owner = Email, MoneyFromRank = 17, Players = [new() { Name = "Roster linked player", Role = Role.Forward,
                    RealTeam = new() { Name = "Genoa", Abbreviation = "GEN" }, Price = 41, Revenue = 58, Status = PlayerInTeamStatus.Sold, Position = FantaSoccerRole.Tribune }] },
                new() { Group = group, BasketId = "same-local-basket", Year = 15, Email = Email }))));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await using var database = fixture.CreateContext(); var report = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal("gen", (await database.PlayerSeasons.SingleAsync()).ClubId);
        var vote = await database.FootballVotes.SingleAsync(); Assert.Null(vote.ClubId); Assert.Equal(6, vote.Value); Assert.True(vote.HasVote); Assert.False(vote.IsActive);
        var recovery = Assert.Single(report.RecoveredRosterPlayers!.Where(x => x.FilledAnnualClub));
        Assert.False(recovery.CreatedGlobalPlayer); Assert.Equal(15, recovery.SeasonIndex); Assert.Equal(0, report.UnresolvedAnnualPlayerClubs);
        Assert.All(await database.RosterSnapshots.AsNoTracking().AsSplitQuery().ToArrayAsync(), x =>
        {
            Assert.Equal(17, x.MoneyFromRank); var row = Assert.Single(x.Players); Assert.Equal("gen", row.ClubId); Assert.Equal(41, row.Price); Assert.Equal(58, row.Revenue);
        });
        Assert.Empty(await database.ClubSeasons.ToArrayAsync());
        var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump); Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
    }

    [CoreFact]
    public async Task Competing_season_roster_clubs_leave_annual_club_unknown_instead_of_selecting_the_first_or_current_team()
    {
        var votes = await ExtraContainerAsync("official");
        await votes.GetBlobClient("source").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<VotedRealPlayerWrapper, YearDayKey>.Default(new()
            { Players = [new() { Name = "Ambiguous club player", RealTeam = null!, Role = Role.Forward, Vote = new() { Value = 7, HasVote = true } }] },
            new() { Year = 15, Day = 2 }))));
        var teams = await ExtraContainerAsync("team");
        foreach (var (group, club) in new[] { ("first", "Genoa"), ("second", "Inter") })
            await teams.GetBlobClient(group).UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<Team, TeamKey>.Default(new()
                { Name = "Historical roster", Owner = Email, Players = [new() { Name = "Ambiguous club player", Role = Role.Forward,
                    RealTeam = new() { Name = club }, Price = 35, Position = FantaSoccerRole.Forward }] },
                new() { Group = group, BasketId = "same-local-basket", Year = 15, Email = Email }))));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await using var database = fixture.CreateContext(); var report = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Null((await database.PlayerSeasons.SingleAsync()).ClubId); Assert.Null((await database.FootballVotes.SingleAsync()).ClubId);
        Assert.Equal(1, report.UnresolvedAnnualPlayerClubs); Assert.Equal(1, report.AmbiguousRosterClubRecoveries);
        Assert.DoesNotContain(report.RecoveredRosterPlayers!, x => x.FilledAnnualClub);
        Assert.Equal(new[] { "gen", "int" }, (await database.RosterSnapshots.AsNoTracking().AsSplitQuery().ToArrayAsync()).Select(x => x.Players.Single().ClubId).Order());
    }

    [CoreFact]
    public async Task Daily_and_opening_rosters_do_not_select_an_unknown_annual_current_club()
    {
        _first.Leagues[0].Years.Add(new() { Year = 16, Settings = LeagueSetting.Default }); await PutGroupAsync(_first);
        var votes = await ExtraContainerAsync("live");
        foreach (var year in new[] { 15, 16 })
            await votes.GetBlobClient(year.ToString()).UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<VotedRealPlayerWrapper, YearDayKey>.Default(new()
                { Players = [new() { Name = "Nonseason fixture player", RealTeam = null!, Role = Role.Forward, Vote = new() { Value = 6, HasVote = true } }] },
                new() { Year = year, Day = 2 }))));
        var team = new Team { Name = "Nonseason historical roster", Owner = Email, Players = [new() { Name = "Nonseason fixture player", Role = Role.Forward,
            RealTeam = new() { Name = "Genoa" }, Price = 38, Position = FantaSoccerRole.Forward }] };
        var daily = await ExtraContainerAsync("dailyteams");
        await daily.GetBlobClient("source").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<Team, TeamDayKey>.Default(team,
            new() { Group = "first", BasketId = "same-local-basket", Year = 15, Email = Email, Day = 2 }))));
        var opening = await ExtraContainerAsync("openingdailyteams");
        await opening.GetBlobClient("source").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<Team, OpeningTeamDayKey>.Default(team,
            new() { Group = "first", BasketId = "same-local-basket", Year = 16, Email = Email, Day = 2, League = "league" }))));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await using var database = fixture.CreateContext(); var report = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.All(await database.PlayerSeasons.ToArrayAsync(), x => Assert.Null(x.ClubId));
        Assert.All(await database.FootballVotes.ToArrayAsync(), x => Assert.Null(x.ClubId));
        Assert.Equal(2, report.UnresolvedAnnualPlayerClubs); Assert.DoesNotContain(report.RecoveredRosterPlayers!, x => x.FilledAnnualClub);
        Assert.Equal(2, await database.RosterSnapshots.CountAsync()); Assert.Empty(await database.ClubSeasons.ToArrayAsync());
    }

    [CoreFact]
    public async Task A_known_annual_club_is_not_erased_by_minimal_votes_and_other_missing_club_contexts_stay_strict()
    {
        var catalogue = await ExtraContainerAsync("realplayerswrapper");
        await catalogue.GetBlobClient("15").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<RealPlayersWrapper, int>.Default(new()
            { Players = [new() { Name = "Known annual player", Role = Role.GoalKeeper, RealTeam = new() { Name = "Genoa" }, IsActive = true }] }, 15))));
        var votes = await ExtraContainerAsync("live");
        await votes.GetBlobClient("source").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<VotedRealPlayerWrapper, YearDayKey>.Default(new()
            { Players = [new() { Name = "Known annual player", RealTeam = null!, Role = Role.GoalKeeper, IsActive = false, Vote = new() { Value = 6, HasVote = true } }] },
            new() { Year = 15, Day = 1 }))));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await using var database = fixture.CreateContext(); var report = await new CoreDatasetImporter(database).ImportAsync(_dump);
        var annual = await database.PlayerSeasons.SingleAsync(); Assert.Equal("gen", annual.ClubId); Assert.True(annual.IsActive);
        Assert.Null((await database.FootballVotes.SingleAsync()).ClubId); Assert.Equal(0, report.UnresolvedAnnualPlayerClubs);
        await votes.DeleteBlobAsync("source"); await catalogue.DeleteBlobAsync("15");
        foreach (var container in new[] { "realplayerswrapper", "team", "chancedrealplayerwrapper", "statplayerswrapper" })
        {
            var sample = ClubDiagnosticPlayer(false); sample.RealTeam = null!;
            object key = container == "team" ? new TeamKey { Group = "first", BasketId = "same-local-basket", Year = 15, Email = Email }
                : container == "chancedrealplayerwrapper" ? new YearDayKey { Year = 15, Day = 2 } : 15;
            object value = container == "team" ? new Team { Name = "Strict roster", Owner = Email, Players = [sample] }
                : new RealPlayersWrapper { Players = [sample] };
            // Analyze directly: this test already has committed SQL to protect.
            var source = _extraContainers.Contains(container) ? _storage.Blobs.GetBlobContainerClient(container) : await ExtraContainerAsync(container);
            await source.GetBlobClient("invalid-source").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(new { k = key, v = value })));
            await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
            var failure = await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump));
            Assert.Equal("missing_real_club", failure.Message);
            Assert.Equal("gen", (await database.PlayerSeasons.AsNoTracking().SingleAsync()).ClubId);
            await source.DeleteBlobAsync("invalid-source");
        }
    }
}

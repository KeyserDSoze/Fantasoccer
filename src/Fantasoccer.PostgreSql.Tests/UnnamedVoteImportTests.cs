using System.Security.Cryptography;
using System.Text;
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
    private static string LegacyVoteFixtureEnvelope(string source, int year, string rows, bool historical)
    {
        var wrapper = "{\"p\":[" + rows + "]}";
        var body = source == "pushnotificationsent"
            ? "{\"" + (historical ? "MatchEventVotes" : "matchEventVotes") + "\":" + wrapper +
                ",\"" + (historical ? "MatchEventSequence" : "matchEventSequence") + "\":7}"
            : wrapper;
        return "{\"" + (historical ? "Key" : "k") + "\":{\"y\":" + year + ",\"d\":1},\"" +
            (historical ? "Value" : "v") + "\":" + body + "}";
    }

    [CoreFact]
    public async Task Unnamed_35_of_331_votes_are_excluded_across_years_and_sources_without_losing_named_votes_or_changing_source_bytes()
    {
        var players = Enumerable.Range(0, 296).Select(index => new VotedRealPlayer
        {
            Name = "Named fixture keeper " + new string([(char)('a' + index / 676), (char)('a' + index / 26 % 26), (char)('a' + index % 26)]),
            RealTeam = null!, Role = Role.GoalKeeper, IsActive = false, Visible = true,
            Vote = new() { Role = Role.GoalKeeper, Value = 6, HasVote = true, IsFinal = true, Goal = index % 2, Assist = index % 3, IsIn = true }
        }).ToArray();
        const string unnamed = """
            {"v":{"r":-1,"v":6,"i":true,"g":0,"p":0,"a":0,"s":0,"d":0,"w":0,"o":0,"t":0,"h":true,"u":false,"n":true},"n":"","t":{"n":"None"},"r":-1,"a":true,"unknown":"private-vote-payload-sentinel"}
            """;
        var rows = string.Join(",", players.Take(12).Select(x => JsonSerializer.Serialize(x))
            .Concat(Enumerable.Repeat(unnamed, 35)).Concat(players.Skip(12).Select(x => JsonSerializer.Serialize(x))));
        var originals = new List<(Azure.Storage.Blobs.BlobClient Blob, string Json, Azure.ETag ETag)>();
        foreach (var source in new[] { "live", "notification", "official", "pushnotificationsent" })
        {
            var container = await ExtraContainerAsync(source);
            foreach (var year in new[] { 10, 12 })
            {
                var blob = container.GetBlobClient("private-vote-source@example.test/" + year);
                var json = LegacyVoteFixtureEnvelope(source, year, rows, historical: year == 10);
                await blob.UploadAsync(BinaryData.FromString(json)); originals.Add((blob, json, (await blob.GetPropertiesAsync()).Value.ETag));
            }
        }
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var (dataset, plan) = await CoreDatasetImporter.AnalyzeAsync(_dump);
        var analysis = CoreDatasetImporter.Report(dataset, plan);
        await using var database = fixture.CreateContext();
        var report = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(analysis.SkippedUnnamedVotes, report.SkippedUnnamedVotes);
        Assert.Equal(296, await database.Players.CountAsync()); Assert.Equal(592, await database.PlayerSeasons.CountAsync());
        // Provisional live votes are cache data now: official and notification rows only.
        Assert.Equal(1184, await database.FootballVotes.CountAsync());
        foreach (var year in new[] { 10, 12 })
        foreach (var source in Enum.GetValues<FootballVoteSource>().Where(x => x != FootballVoteSource.Live))
        {
            var saved = await database.FootballVotes.Where(x => x.SeasonIndex == year && x.Source == source).ToArrayAsync();
            Assert.Equal(296, saved.Length);
            foreach (var player in players)
            {
                var row = saved.Single(x => x.PlayerId == player.Key);
                Assert.Null(row.ClubId); Assert.True(row.HasVote); Assert.Equal(6, row.Value); Assert.True(row.IsFinal); Assert.True(row.IsIn);
                Assert.False(row.IsActive); Assert.True(row.IsVisible); Assert.Equal(player.Vote.Goal, row.Goals); Assert.Equal(player.Vote.Assist, row.Assists);
            }
        }
        var checkpoints = await database.PushDayCheckpoints.AsNoTracking().AsSplitQuery().ToArrayAsync();
        Assert.Equal(2, checkpoints.Length);
        Assert.All(checkpoints, x =>
        {
            Assert.True(x.HasMatchEventVotes); Assert.Equal(7, x.MatchEventSequence); Assert.Equal(296, x.Votes.Count);
            Assert.Equal(Enumerable.Range(0, 296), x.Votes.OrderBy(vote => vote.Ordinal).Select(vote => vote.Ordinal));
            Assert.Equal(players.Select(player => player.Key), x.Votes.OrderBy(vote => vote.Ordinal).Select(vote => vote.PlayerId));
            Assert.All(x.Votes, vote => { Assert.True(vote.HasVote); Assert.Equal(6, vote.Value); Assert.False(vote.IsActive); Assert.Null(vote.ClubId); });
        });
        Assert.Equal(8, report.SkippedUnnamedVotes!.Length);
        Assert.All(report.SkippedUnnamedVotes, x => { Assert.Equal(331, x.TotalEntries); Assert.Equal(35, x.Records); Assert.Equal(35, x.WithHasVote); Assert.Equal(1, x.Day); });
        Assert.Contains(report.SkippedUnnamedVotes, x => x.Container == "official" && x.SeasonIndex == 10 && x.FirstJsonPath == "$.Value.p[12]");
        Assert.Contains(report.SkippedUnnamedVotes, x => x.Container == "pushnotificationsent" && x.SeasonIndex == 10 && x.FirstJsonPath == "$.Value.MatchEventVotes.p[12]");
        Assert.Contains(report.SkippedUnnamedVotes, x => x.Container == "pushnotificationsent" && x.SeasonIndex == 12 && x.FirstJsonPath == "$.v.matchEventVotes.p[12]");
        Assert.All(report.MissingVoteClubSnapshots!, x => Assert.Equal(296, x.Records));
        Assert.Empty(await database.Clubs.ToArrayAsync()); Assert.Empty(await database.ClubSeasons.ToArrayAsync());
        Assert.Empty(await database.UnresolvedPlayerCatalogEntries.ToArrayAsync()); Assert.Empty(await database.PushDeliveries.ToArrayAsync());
        Assert.False(report.FullMigrationComplete);
        var formatted = JsonSerializer.Serialize(report);
        foreach (var secret in new[] { "Named fixture keeper", "private-vote-source@example.test", "None", "private-vote-payload-sentinel" })
            Assert.DoesNotContain(secret, formatted);
        var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(repeated.Documents, repeated.SkippedDocuments); Assert.Equal(report.SkippedUnnamedVotes, repeated.SkippedUnnamedVotes);
        Assert.Equal(1184, await database.FootballVotes.CountAsync());
        foreach (var (blob, json, etag) in originals)
        {
            Assert.Equal(etag, (await blob.GetPropertiesAsync()).Value.ETag); Assert.Equal(json, (await blob.DownloadContentAsync()).Value.Content.ToString());
            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
            var skipped = report.SkippedUnnamedVotes.Single(x => x.DocumentSha256 == hash && x.Container == blob.BlobContainerName);
            Assert.Equal(json, await File.ReadAllTextAsync(Path.Combine(_dump, skipped.ArchiveReference)));
        }
    }

    [CoreFact]
    public async Task All_unnamed_vote_documents_keep_dataset_roots_and_push_checkpoint_metadata_but_create_no_players_clubs_or_votes()
    {
        // All are objects without a usable name; their vote/club/role metadata
        // cannot turn them into football identities. A null array item is NOT
        // included in this operator-approved policy.
        const string rows = """
            {}, {"n":null}, {"n":" \t "}, {"n":"","v":null}, {"n":"","r":99,"t":{"n":"None"},"v":{"v":6,"h":true}}
            """;
        foreach (var source in new[] { "live", "notification", "official", "pushnotificationsent" })
        {
            var container = await ExtraContainerAsync(source);
            var json = LegacyVoteFixtureEnvelope(source, 15, rows, historical: true);
            if (source == "pushnotificationsent") json = json.Replace("\"p\":[", "\"P\":[", StringComparison.Ordinal);
            await container.GetBlobClient("all-unnamed").UploadAsync(BinaryData.FromString(json));
        }
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await using var database = fixture.CreateContext(); var report = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Empty(await database.Players.ToArrayAsync()); Assert.Empty(await database.PlayerSeasons.ToArrayAsync());
        Assert.Empty(await database.Clubs.ToArrayAsync()); Assert.Empty(await database.FootballVotes.ToArrayAsync());
        Assert.Equal(2, await database.FootballDatasets.CountAsync(x => x.Day == 1)); // no SQL root for the live snapshot
        var checkpoint = await database.PushDayCheckpoints.AsNoTracking().AsSplitQuery().SingleAsync();
        Assert.True(checkpoint.HasMatchEventVotes); Assert.Equal(7, checkpoint.MatchEventSequence); Assert.Empty(checkpoint.Votes);
        Assert.Equal(4, report.SkippedUnnamedVotes!.Length);
        Assert.All(report.SkippedUnnamedVotes, x => { Assert.Equal(5, x.TotalEntries); Assert.Equal(5, x.Records); Assert.Equal(1, x.WithHasVote); });
        Assert.Contains(report.SkippedUnnamedVotes, x => x.FirstJsonPath == "$.Value.MatchEventVotes.P[0]");
        Assert.Empty(report.MissingVoteClubSnapshots!);
        var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
    }

    [CoreFact]
    public async Task Unnamed_vote_exclusion_does_not_hide_null_entries_invalid_named_keys_roles_missing_votes_or_clubs()
    {
        foreach (var source in new[] { "live", "notification", "official", "pushnotificationsent" })
        {
            var container = await ExtraContainerAsync(source);
            foreach (var (row, code, reason) in new (string, string, string?)[]
            {
                ("null", "invalid_player", "null_player"),
                ("{\"n\":\"!!!\",\"r\":3,\"t\":null,\"v\":{}}", "invalid_player", "empty_player_key"),
                ("{\"n\":\"Named invalid role\",\"r\":99,\"t\":null,\"v\":{}}", "invalid_player", "role_out_of_range"),
                ("{\"n\":\"Named missing vote\",\"r\":3,\"t\":null}", source == "pushnotificationsent" ? "missing_push_baseline_vote" : "missing_player_vote", null),
                ("{\"n\":\"Named invalid club\",\"r\":3,\"t\":{\"n\":\"None\"},\"v\":{}}", "invalid_club_code", null)
            })
            {
                await container.GetBlobClient("invalid-source").UploadAsync(BinaryData.FromString(LegacyVoteFixtureEnvelope(source, 15, "{\"n\":\"\"}," + row, historical: false)), overwrite: true);
                await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
                var fault = await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump));
                Assert.Equal(code, fault.Message);
                if (reason is not null)
                {
                    Assert.Contains(reason, fault.PlayerValidation!.Reasons);
                    Assert.EndsWith("[1]", fault.PlayerValidation.JsonPath);
                }
            }
            await container.DeleteBlobAsync("invalid-source");
        }
        await using var database = fixture.CreateContext();
        Assert.Empty(await database.Players.ToArrayAsync()); Assert.Empty(await database.CoreImportCheckpoints.ToArrayAsync());
    }

    [CoreFact]
    public async Task Excluding_unnamed_votes_does_not_recalculate_imported_calendar_results_or_current_rank()
    {
        // One user controls at most one team per league: the second owner is no
        // longer also a co-owner of the first team in this fixture.
        _first.Baskets[0].Years[0].Teams[0].AdditionalOwners = [];
        _first.Baskets[0].Years[0].Teams.Add(new() { Name = "Second team", Owner = OtherEmail, AdditionalOwners = [] });
        await PutGroupAsync(_first);
        var key = new LeagueKey { Group = "first", League = "league", Year = 15 };
        var calendar = await ExtraContainerAsync("calendar");
        await calendar.GetBlobClient("saved").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<Calendar, LeagueKey>.Default(new()
        {
            Year = 15, Rounds = new() { ["Historical stage"] = [new() { Number = 7, SerieADay = 1, Games = [new()
            {
                Id = "saved-fixture", Home = "Saved home", HomeOwner = Email, Away = "Saved away", AwayOwner = OtherEmail,
                Result = new() { Home = new(74.25f, true, false, false), Away = new(67, false, true, false), HomeGoal = 3, AwayGoal = 1 }
            }] }] }
        }, key))));
        Rank SavedRank(float points) => new() { SerieADay = 1, RankedTeams = new() { ["Historical stage"] =
            [new() { Name = "Saved home", Owner = Email, Point = points, ValuePoint = 711.5f, Money = 123, PlusMoney = 8 }] } };
        var rank = await ExtraContainerAsync("rank");
        await rank.GetBlobClient("saved").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<Rank, LeagueKey>.Default(SavedRank(20.5f), key))));
        var daily = await ExtraContainerAsync("dailyrank");
        await daily.GetBlobClient("saved").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<Rank, YearDayLeague>.Default(SavedRank(3),
            new() { Group = key.Group, League = key.League, Year = key.Year, Day = 1 }))));
        var official = await ExtraContainerAsync("official");
        await official.GetBlobClient("unnamed").UploadAsync(BinaryData.FromString(LegacyVoteFixtureEnvelope("official", 15,
            "{\"n\":\"\",\"r\":-1,\"t\":{\"n\":\"None\"},\"v\":{\"v\":6,\"h\":true,\"i\":true}}", historical: true)));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await using var database = fixture.CreateContext(); var report = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(1, Assert.Single(report.SkippedUnnamedVotes!).Records);
        Assert.Empty(await database.FootballVotes.ToArrayAsync()); Assert.Empty(await database.Players.ToArrayAsync());
        foreach (var run in Enumerable.Range(0, 2))
        {
            var game = await database.FantasyMatches.AsNoTracking().Include(x => x.Matchday).SingleAsync();
            Assert.Equal(7, game.Matchday.FantasyTurn); Assert.Equal(1, game.Matchday.SerieADay);
            Assert.Equal(74.25f, game.Result!.Home!.Value); Assert.Equal(3, game.Result.HomeGoals); Assert.Equal(1, game.Result.AwayGoals);
            var snapshots = await database.FantasyStandingSnapshots.AsNoTracking().AsSplitQuery().OrderBy(x => x.SnapshotDay).ToArrayAsync();
            Assert.Single(snapshots); Assert.Equal(0, snapshots[0].SnapshotDay);
            Assert.Equal(20.5f, Assert.Single(Assert.Single(snapshots[0].Stages).Teams).Points);
            Assert.All(snapshots, x =>
            {
                var team = Assert.Single(Assert.Single(x.Stages).Teams);
                Assert.Equal(711.5f, team.ValuePoints); Assert.Equal(123, team.Money); Assert.Equal(8, team.PlusMoney);
            });
            if (run == 0)
            {
                var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
                Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
            }
        }
    }

    [CoreFact]
    public async Task Skipping_unnamed_votes_keeps_named_duplicate_vote_checks_and_original_baseline_order()
    {
        const string named = "{\"n\":\"Named duplicate fixture\",\"r\":3,\"t\":null,\"v\":{\"v\":6,\"h\":true}}";
        var rows = named + ",{\"n\":\"\"}," + named;
        foreach (var source in new[] { "live", "notification", "official" })
        {
            var container = await ExtraContainerAsync(source);
            await container.GetBlobClient("duplicates").UploadAsync(BinaryData.FromString(LegacyVoteFixtureEnvelope(source, 15, rows, historical: false)));
            await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
            var fault = await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump));
            Assert.Equal("duplicate_or_invalid_vote", fault.Message);
            await container.DeleteBlobAsync("duplicates");
        }
        var push = await ExtraContainerAsync("pushnotificationsent");
        await push.GetBlobClient("duplicates").UploadAsync(BinaryData.FromString(LegacyVoteFixtureEnvelope("pushnotificationsent", 15, rows, historical: false)));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await using var database = fixture.CreateContext(); var report = await new CoreDatasetImporter(database).ImportAsync(_dump);
        var checkpoint = await database.PushDayCheckpoints.AsNoTracking().AsSplitQuery().SingleAsync();
        Assert.Equal(2, checkpoint.Votes.Count); Assert.Equal(new[] { 0, 1 }, checkpoint.Votes.Select(x => x.Ordinal).Order());
        Assert.All(checkpoint.Votes, x => Assert.Equal("namedduplicatefixture", x.PlayerId));
        Assert.Equal(1, Assert.Single(report.SkippedUnnamedVotes!).Records);
    }
}

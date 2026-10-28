using System.Globalization;
using System.Text.Json;
using Fantasoccer.Domain;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.StorageMigration;
using RepositoryFramework;

namespace Fantasoccer.DataMigration;

public sealed partial class CoreDatasetImporter
{
    private static void LoadFootballSamples(VerifiedJsonDataset dataset, CorePlan plan,
        Func<RealPlayer, int, bool, PlayerSource, PlayerSeason> player, Func<RealTeam?, ClubSource, string> club, Action<int> season,
        Action<string, int, int> root, Func<string?, ClubSource, string> rankClub, Action<string, int, ClubMembershipSource> membership, Func<string, string, Guid> map)
    {
        var postponed = LegacyPostponedClubs(dataset);
        foreach (var document in dataset.Documents.Where(x => x.Container is "live" or "notification" or "official"))
        {
            var entity = document.Envelope.Deserialize<Entity<VotedRealPlayerWrapper, YearDayKey>>() ?? throw new CoreImportException("invalid_votes_envelope");
            var key = entity.Key ?? throw new CoreImportException("missing_votes_key");
            CheckDay(key); season(key.Year); root(document.Container, key.Year, key.Day);
            var source = document.Container == "live" ? FootballVoteSource.Live : document.Container == "official" ? FootballVoteSource.Official : FootballVoteSource.Notification;
            var missingClubs = 0; string? firstMissingClub = null;
            var unnamed = 0; var unnamedWithVote = 0; string? firstUnnamed = null;
            var samples = entity.Value?.Players ?? throw new CoreImportException("missing_vote_players");
            var inactive = InactiveSubstitutes(document, samples);
            var variants = ClassifyPoliticalVotes(document, samples, key.Year, key.Day, postponed, inactive);
            var political = 0; var paired = 0; string? firstPolitical = null;
            var removed = 0; var removedWithCounterpart = 0; string? firstRemoved = null;
            var keptKeys = samples.Where((value, index) => value is not null && !inactive[index] && !IsUnnamedVote(value)).Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
            // Root has already enforced a unique source/year/day document.
            // Retain original source ordinals, not SQL ordinals after skips.
            var votes = new Dictionary<string, int>(StringComparer.Ordinal);
            var identities = new Dictionary<string, int>(StringComparer.Ordinal);
            // Actual evidence creates a missing annual row before a legacy
            // substitute does. Source ordinals/JSON are never reordered in storage.
            foreach (var (value, index) in samples.Select((value, index) => (value, index)).OrderBy(x => variants[x.index]))
            {
                var origin = PlayerOrigin(document, $".p[{index}]");
                if (IsUnnamedVote(value))
                {
                    unnamed++; if (value.Vote?.HasVote == true) unnamedWithVote++;
                    firstUnnamed ??= origin.JsonPath;
                    continue;
                }
                if (inactive[index])
                {
                    // Excluded before any player/annual row is created from
                    // its placeholder metadata; the dump keeps the original.
                    removed++; if (keptKeys.Contains(value.Key)) removedWithCounterpart++;
                    firstRemoved ??= origin.JsonPath;
                    continue;
                }
                var identity = player(value, key.Year, false, origin);
                var vote = value.Vote ?? throw new CoreImportException("missing_player_vote");
                var legacyPolitical = variants[index];
                var duplicate = !legacyPolitical && !votes.TryAdd(identity.PlayerId, index);
                var incompatible = identities.TryGetValue(identity.PlayerId, out var firstIndex) &&
                    !SamePoliticalIdentity(value, samples[firstIndex], origin, PlayerOrigin(document, $".p[{firstIndex}]"));
                if (duplicate || incompatible || !float.IsFinite(vote.Value))
                {
                    var raw = Value(document.Envelope).GetProperty("p");
                    var previous = duplicate ? votes[identity.PlayerId] : incompatible ? firstIndex : (int?)null;
                    throw VoteFault("duplicate_or_invalid_vote", origin, key.Year, key.Day, value, raw[index],
                        previous is { } ordinal ? PlayerOrigin(document, $".p[{ordinal}]") : null,
                        previous is { } first ? samples[first] : null, previous is { } earlier ? raw[earlier] : null);
                }
                identities.TryAdd(identity.PlayerId, index);
                var clubCode = VoteClub(value, origin, club);
                if (legacyPolitical)
                {
                    political++; if (votes.ContainsKey(identity.PlayerId)) paired++;
                    firstPolitical ??= origin.JsonPath;
                    plan.LegacyPoliticalVoteEntries.Add(new()
                    {
                        Id = map("legacy-political-vote", Composite(document.Sha256, index.ToString(CultureInfo.InvariantCulture))),
                        SourceId = dataset.Manifest.SourceId, DocumentSha256 = document.Sha256, ArchiveReference = DocumentArchiveReference(document),
                        JsonPath = origin.JsonPath, SourceOrdinal = index, PlayerId = identity.PlayerId, SeasonIndex = key.Year, Day = key.Day,
                        ClubId = clubCode, OriginalPayloadJson = Value(document.Envelope).GetProperty("p")[index].GetRawText()
                    });
                    continue;
                }
                if (clubCode is null) { missingClubs++; firstMissingClub ??= ClubOrigin(origin).JsonPath; }
                plan.Votes.Add(new() { PlayerId = identity.PlayerId, SeasonIndex = key.Year, Day = key.Day, Source = source,
                    ClubId = clubCode, PlayerRole = (int)value.Role, IsActive = value.IsActive, IsVisible = value.Visible,
                    VoteRole = (int)vote.Role, Value = vote.Value, IsFinal = vote.IsFinal, Goals = vote.Goal, Penalties = vote.Penalty,
                    Assists = vote.Assist, StoppedPenalties = vote.StoppedPenalty, SufferedGoals = vote.SufferedGoal,
                    WrongedPenalties = vote.WrongedPenalty, OwnGoals = vote.OwnGoal, Status = (int)vote.Status, ManOfTheMatch = vote.ManOfTheMatch,
                    HasVote = vote.HasVote, IsOut = vote.IsOut, IsIn = vote.IsIn, Injured = vote.Injured });
            }
            if (missingClubs > 0) plan.MissingVoteClubSnapshots.Add(new(document.Sha256, document.Container, DocumentArchiveReference(document),
                key.Year, key.Day, missingClubs, firstMissingClub!));
            if (unnamed > 0) plan.SkippedUnnamedVotes.Add(new(document.Sha256, document.Container, DocumentArchiveReference(document),
                key.Year, key.Day, samples.Count, unnamed, unnamedWithVote, firstUnnamed!));
            if (political > 0) plan.LegacyPoliticalVotes.Add(new(document.Sha256, DocumentArchiveReference(document), key.Year, key.Day, political, paired, firstPolitical!));
            if (removed > 0) plan.SkippedInactiveSubstitutes.Add(new(document.Sha256, DocumentArchiveReference(document), key.Year, key.Day,
                removed, removedWithCounterpart, firstRemoved!));
        }
        var chances = new HashSet<(string, int, int)>();
        foreach (var document in dataset.Documents.Where(x => x.Container == "chancedrealplayerwrapper"))
        {
            var entity = document.Envelope.Deserialize<Entity<ChancedRealPlayerWrapper, YearDayKey>>() ?? throw new CoreImportException("invalid_chance_envelope");
            var key = entity.Key ?? throw new CoreImportException("missing_chance_key");
            CheckDay(key); season(key.Year); root(document.Container, key.Year, key.Day);
            var original = Value(document.Envelope).GetProperty("p");
            var index = 0;
            var players = entity.Value?.Players ?? throw new CoreImportException("missing_chance_players");
            var unusable = 0; string? firstUnusable = null;
            foreach (var value in players)
            {
                var origin = PlayerOrigin(document, $".p[{index}]");
                if (UnusablePlayerName(value)) { unusable++; firstUnusable ??= origin.JsonPath; index++; continue; }
                var identity = player(value, key.Year, false, origin);
                var chance = value.Chance ?? throw new CoreImportException("missing_chance");
                if (!chances.Add((identity.PlayerId, key.Year, key.Day))) throw new CoreImportException("duplicate_chance");
                var row = new FootballChance { PlayerId = identity.PlayerId, SeasonIndex = key.Year, Day = key.Day,
                    ClubId = club(value.RealTeam, ClubOrigin(origin)), PlayerRole = (int)value.Role, IsActive = value.IsActive, IsVisible = value.Visible,
                    Fantagazzetta = chance.Fantagazzetta, Gazzetta = chance.Gazzetta, Mediaset = chance.Mediaset, Sky = chance.Sky,
                    Status = (int)chance.Status, Description = chance.Description };
                // These fields existed in older documents but no longer in the
                // CLR Chance model. Preserve them explicitly instead of dropping them.
                var raw = original[index++].GetProperty("c");
                if (raw.TryGetProperty("r", out var trend) && trend.ValueKind == JsonValueKind.Number) row.LegacyTrend = trend.GetInt32();
                if (raw.TryGetProperty("l", out var game) && game.ValueKind == JsonValueKind.Object)
                {
                    row.LegacyLastGameDay = game.GetProperty("d").GetInt32();
                    row.LegacyLastGameVote = game.GetProperty("v").ValueKind == JsonValueKind.Null ? null : game.GetProperty("v").GetSingle();
                    row.LegacyLastGamePositiveness = game.GetProperty("p").GetInt32();
                }
                plan.Chances.Add(row);
            }
            if (unusable > 0) plan.SkippedUnusablePlayerRows.Add(new(document.Sha256, document.Container, DocumentArchiveReference(document),
                key.Year, key.Day, players.Count, unusable, firstUnusable!));
        }
        var statistics = new HashSet<(string, int)>();
        foreach (var document in dataset.Documents.Where(x => x.Container == "statplayerswrapper"))
        {
            var entity = document.Envelope.Deserialize<Entity<StatPlayersWrapper, int>>() ?? throw new CoreImportException("invalid_statistics_envelope");
            season(entity.Key); root(document.Container, entity.Key, 0);
            var players = entity.Value?.Players ?? throw new CoreImportException("missing_statistics_players");
            var unusable = 0; string? firstUnusable = null;
            foreach (var (value, index) in players.Select((value, index) => (value, index)))
            {
                var origin = PlayerOrigin(document, $".p[{index}]");
                if (UnusablePlayerName(value)) { unusable++; firstUnusable ??= origin.JsonPath; continue; }
                var identity = player(value, entity.Key, false, origin);
                if (!statistics.Add((identity.PlayerId, entity.Key)) || !float.IsFinite(value.Summatory) || !float.IsFinite(value.FantaSummatory))
                    throw new CoreImportException("duplicate_or_invalid_statistics");
                var row = new FootballPlayerStatistic { PlayerId = identity.PlayerId, SeasonIndex = entity.Key,
                    ClubId = club(value.RealTeam, ClubOrigin(origin)), PlayerRole = (int)value.Role, IsActive = value.IsActive, IsVisible = value.Visible,
                    Summatory = value.Summatory, FantaSummatory = value.FantaSummatory, WithVote = value.WithVote, WithoutVote = value.WithoutVote,
                    NoPlayed = value.NoPlayed, WithSpecial = value.WithSpecial, Goals = value.Goal, Penalties = value.Penalty, Assists = value.Assist,
                    StoppedPenalties = value.StoppedPenalty, SufferedGoals = value.SufferedGoal, WrongedPenalties = value.WrongedPenalty,
                    OwnGoals = value.OwnGoal, YellowCards = value.YellowCard, RedCards = value.RedCard, EnoughVotes = value.IsEnough,
                    ManOfTheMatch = value.ManOfTheMatch, Injured = value.Injured, HasDescription = value.Description != null,
                    DescriptionTop = value.Description?.Top, DescriptionDown = value.Description?.Down, HasGameHistory = value.AllGames != null };
                var days = new HashSet<int>();
                foreach (var (game, ordinal) in (value.AllGames ?? []).Select((item, ordinal) => (item, ordinal)))
                {
                    if (game.SerieADay is < 1 or > 38 || !days.Add(game.SerieADay) || game.Vote is { } v && !float.IsFinite(v)) throw new CoreImportException("invalid_statistic_game");
                    row.Games.Add(new() { Day = game.SerieADay, Ordinal = ordinal, Vote = game.Vote, Positiveness = game.Positiveness });
                }
                plan.Statistics.Add(row);
            }
            if (unusable > 0) plan.SkippedUnusablePlayerRows.Add(new(document.Sha256, document.Container, DocumentArchiveReference(document),
                entity.Key, 0, players.Count, unusable, firstUnusable!));
        }
        foreach (var document in dataset.Documents.Where(x => x.Container == "realrank"))
        {
            var entity = document.Envelope.Deserialize<Entity<RealRank, int>>() ?? throw new CoreImportException("invalid_real_rank_envelope");
            season(entity.Key); root(document.Container, entity.Key, 0);
            var clubs = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (value, ordinal) in (entity.Value?.Teams ?? throw new CoreImportException("missing_real_rank")).Select((item, ordinal) => (item, ordinal)))
            {
                if (!float.IsFinite(value.Point)) throw new CoreImportException("invalid_real_rank_points");
                // Legacy standings store a display name only. Link an exact,
                // unambiguous label already carrying a verified code; never
                // manufacture a code from the first letters of that label.
                var id = rankClub(value.Name, ClubOrigin(document, $".r[{ordinal}]"));
                if (!clubs.Add(id)) throw new CoreImportException("duplicate_real_rank_club");
                membership(id, entity.Key, ClubMembershipSource.Standings);
                plan.ClubRanks.Add(new() { SeasonIndex = entity.Key, ClubId = id, Ordinal = ordinal, Points = value.Point,
                    Victories = value.Victories, Draws = value.Draws, Defeats = value.Defeats, GoalsFor = value.Goal, GoalsAgainst = value.SufferedGoal, Played = value.Played });
            }
        }
    }
    private static JsonElement Value(JsonElement envelope) => envelope.TryGetProperty("v", out var value) ? value : envelope.GetProperty("Value");
    private static void CheckDay(YearDayKey key)
    {
        if (key.Day is < 1 or > 38) throw new CoreImportException("invalid_football_matchday");
    }
}

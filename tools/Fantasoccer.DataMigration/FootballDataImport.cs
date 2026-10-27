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
        Action<string, int, int> root, Func<string?, ClubSource, string> rankClub, Action<string, int, ClubMembershipSource> membership)
    {
        var votes = new HashSet<(string, int, int, FootballVoteSource)>();
        foreach (var document in dataset.Documents.Where(x => x.Container is "live" or "notification" or "official"))
        {
            var entity = document.Envelope.Deserialize<Entity<VotedRealPlayerWrapper, YearDayKey>>() ?? throw new CoreImportException("invalid_votes_envelope");
            var key = entity.Key ?? throw new CoreImportException("missing_votes_key");
            CheckDay(key); season(key.Year); root(document.Container, key.Year, key.Day);
            var source = document.Container == "live" ? FootballVoteSource.Live : document.Container == "official" ? FootballVoteSource.Official : FootballVoteSource.Notification;
            foreach (var (value, index) in (entity.Value?.Players ?? throw new CoreImportException("missing_vote_players")).Select((value, index) => (value, index)))
            {
                var origin = PlayerOrigin(document, $".p[{index}]");
                var identity = player(value, key.Year, false, origin);
                var vote = value.Vote ?? throw new CoreImportException("missing_player_vote");
                if (!votes.Add((identity.PlayerId, key.Year, key.Day, source)) || !float.IsFinite(vote.Value)) throw new CoreImportException("duplicate_or_invalid_vote");
                plan.Votes.Add(new() { PlayerId = identity.PlayerId, SeasonIndex = key.Year, Day = key.Day, Source = source,
                    ClubId = club(value.RealTeam, ClubOrigin(origin)), PlayerRole = (int)value.Role, IsActive = value.IsActive, IsVisible = value.Visible,
                    VoteRole = (int)vote.Role, Value = vote.Value, IsFinal = vote.IsFinal, Goals = vote.Goal, Penalties = vote.Penalty,
                    Assists = vote.Assist, StoppedPenalties = vote.StoppedPenalty, SufferedGoals = vote.SufferedGoal,
                    WrongedPenalties = vote.WrongedPenalty, OwnGoals = vote.OwnGoal, Status = (int)vote.Status, ManOfTheMatch = vote.ManOfTheMatch,
                    HasVote = vote.HasVote, IsOut = vote.IsOut, IsIn = vote.IsIn, Injured = vote.Injured });
            }
        }
        var chances = new HashSet<(string, int, int)>();
        foreach (var document in dataset.Documents.Where(x => x.Container == "chancedrealplayerwrapper"))
        {
            var entity = document.Envelope.Deserialize<Entity<ChancedRealPlayerWrapper, YearDayKey>>() ?? throw new CoreImportException("invalid_chance_envelope");
            var key = entity.Key ?? throw new CoreImportException("missing_chance_key");
            CheckDay(key); season(key.Year); root(document.Container, key.Year, key.Day);
            var original = Value(document.Envelope).GetProperty("p");
            var index = 0;
            foreach (var value in entity.Value?.Players ?? throw new CoreImportException("missing_chance_players"))
            {
                var origin = PlayerOrigin(document, $".p[{index}]");
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
        }
        var statistics = new HashSet<(string, int)>();
        foreach (var document in dataset.Documents.Where(x => x.Container == "statplayerswrapper"))
        {
            var entity = document.Envelope.Deserialize<Entity<StatPlayersWrapper, int>>() ?? throw new CoreImportException("invalid_statistics_envelope");
            season(entity.Key); root(document.Container, entity.Key, 0);
            foreach (var (value, index) in (entity.Value?.Players ?? throw new CoreImportException("missing_statistics_players")).Select((value, index) => (value, index)))
            {
                var origin = PlayerOrigin(document, $".p[{index}]");
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

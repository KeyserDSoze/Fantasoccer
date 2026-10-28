using System.Text.Json;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.StorageMigration;
using RepositoryFramework;

namespace Fantasoccer.DataMigration;

public sealed record LegacyPoliticalVoteDocument(string DocumentSha256, string ArchiveReference, int SeasonIndex, int Day,
    int Records, int WithActualCounterpart, string FirstJsonPath);

public sealed partial class CoreDatasetImporter
{
    private static bool PoliticalShape(VotedRealPlayer? sample, JsonElement raw)
    {
        if (sample?.Vote is not { } vote || sample.Role is < Role.Undefined or > Role.Forward || vote.Role != sample.Role ||
            !vote.HasVote || !vote.IsFinal || vote.Value != 6 || vote.IsIn || vote.IsOut || vote.Goal != 0 || vote.Penalty != 0 ||
            vote.Assist != 0 || vote.StoppedPenalty != 0 || vote.SufferedGoal != 0 || vote.WrongedPenalty != 0 || vote.OwnGoal != 0 ||
            vote.Status != Behaviour.Nothing || vote.ManOfTheMatch || vote.Injured || raw.ValueKind != JsonValueKind.Object ||
            !UniqueProperties(raw) || raw.EnumerateObject().Any(x => x.Name is not ("n" or "t" or "r" or "a" or "vh" or "v")) ||
            !raw.TryGetProperty("v", out var payload) || payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("n", out var entered) || entered.ValueKind != JsonValueKind.False)
            return false;
        return UniqueProperties(payload) && payload.EnumerateObject().All(x => x.Name is "r" or "v" or "i" or "g" or "p" or "a" or "s" or "d" or "w" or "o" or "t" or "c" or "h" or "u" or "n" or "j");
    }

    private static bool UniqueProperties(JsonElement value)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        return value.EnumerateObject().All(x => names.Add(x.Name));
    }

    private static bool SamePoliticalIdentity(VotedRealPlayer first, VotedRealPlayer other, PlayerSource firstOrigin, PlayerSource otherOrigin)
    {
        if (!string.Equals(first.Name, other.Name, StringComparison.OrdinalIgnoreCase) || first.Role != other.Role || first.Vote.Role != other.Vote.Role)
            return false;
        if (first.RealTeam is null || other.RealTeam is null) return first.RealTeam is null && other.RealTeam is null;
        return TryClubCode(first.RealTeam, firstOrigin) is { } code && code == TryClubCode(other.RealTeam, otherOrigin);
    }

    // Classification never replaces the main reader's club diagnostics.
    private static string? TryClubCode(RealTeam? club, PlayerSource origin)
    {
        if (club is null) return null;
        try { return CheckedClubCode(club, ClubOrigin(origin)); }
        catch (CoreImportException) { return null; }
    }

    private sealed record LegacyCalendarIndex(HashSet<(int Year, int Day, string Club)> Postponed,
        Dictionary<(int Year, int Day, string Club), string> Opponents);

    private static LegacyCalendarIndex LegacyPostponedClubs(VerifiedJsonDataset dataset)
    {
        var result = new HashSet<(int, int, string)>();
        var opponents = new Dictionary<(int, int, string), string>();
        foreach (var document in dataset.Documents.Where(x => x.Container == "realcalendar"))
        {
            var calendar = document.Envelope.Deserialize<Entity<RealCalendar, int>>() ?? throw new CoreImportException("invalid_calendar_envelope");
            // Another season's calendar never attests a postponement here.
            if (OtherSeasonCalendar(document, calendar) is not null) continue;
            foreach (var (day, dayIndex) in (calendar.Value?.Days ?? []).Select((value, index) => (value, index)))
            foreach (var (game, gameIndex) in (day.Games ?? []).Select((value, index) => (value, index)))
            {
                var home = CheckedClubCode(game.Home, ClubOrigin(document, $".d[{dayIndex}].g[{gameIndex}].h"));
                var away = CheckedClubCode(game.Away, ClubOrigin(document, $".d[{dayIndex}].g[{gameIndex}].a"));
                opponents.TryAdd((calendar.Key, day.SerieADay, home), away);
                opponents.TryAdd((calendar.Key, day.SerieADay, away), home);
                if (!game.Delayed && !game.WasPostponed) continue;
                result.Add((calendar.Key, day.SerieADay, home));
                result.Add((calendar.Key, day.SerieADay, away));
            }
        }
        return new(result, opponents);
    }

    private static bool[] ClassifyPoliticalVotes(LegacyJsonDocument document, List<VotedRealPlayer> samples, int year, int day,
        LegacyCalendarIndex calendar)
    {
        var variants = new bool[samples.Count];
        if (document.Container != "official") return variants;
        var raw = Value(document.Envelope).GetProperty("p");
        var groups = samples.Select((sample, index) => (sample, index)).Where(x => x.sample is not null && !string.IsNullOrWhiteSpace(x.sample.Name))
            .GroupBy(x => x.sample.Key, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);
        var shaped = groups.Values.SelectMany(x => x).Where(x => PoliticalShape(x.sample, raw[x.index])).ToArray();
        // A substitute is attested by the calendar, or by an actual entry with
        // the same identity, role and club. That evidence attests the club's
        // postponed game for the whole day, so substitutes of squad members who
        // did not play the recovery are not mistaken for genuine 6s.
        var clubs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (sample, index) in shaped)
        {
            var origin = PlayerOrigin(document, $".p[{index}]");
            var code = TryClubCode(sample.RealTeam, origin);
            var paired = groups[sample.Key].Any(other => other.index != index && other.sample.Vote is { HasVote: true, IsFinal: true, IsIn: true } &&
                !PoliticalShape(other.sample, raw[other.index]) && SamePoliticalIdentity(sample, other.sample, origin, PlayerOrigin(document, $".p[{other.index}]")));
            if (paired) variants[index] = true;
            if (code is not null && (paired || calendar.Postponed.Contains((year, day, code)))) clubs.Add(code);
        }
        // A postponed game involves both clubs: the opponent's substitutes are
        // attested by the same calendar game, never by a 6 value alone.
        foreach (var code in clubs.ToArray())
            if (calendar.Opponents.TryGetValue((year, day, code), out var opponent)) clubs.Add(opponent);
        foreach (var (sample, index) in shaped)
            if (TryClubCode(sample.RealTeam, PlayerOrigin(document, $".p[{index}]")) is { } code && clubs.Contains(code)) variants[index] = true;
        return variants;
    }

    private static void AttestImportedPostponements(CorePlan plan)
    {
        var politicalClubs = plan.LegacyPoliticalVoteEntries.Where(x => x.ClubId is not null)
            .Select(x => (x.SeasonIndex, x.Day, x.ClubId)).ToHashSet();
        var actualClubs = plan.Votes.Where(x => x.Source == FootballVoteSource.Official && x.IsFinal && x.HasVote && x.ClubId is not null)
            .Select(x => (x.SeasonIndex, x.Day, x.ClubId)).ToHashSet();
        foreach (var day in plan.Matchdays)
        foreach (var game in day.Matches)
        {
            game.WasPostponed |= game.Delayed || politicalClubs.Contains((game.SeasonIndex, game.SerieADay, game.HomeClubId)) ||
                politicalClubs.Contains((game.SeasonIndex, game.SerieADay, game.AwayClubId));
            // Other games use the runtime vote check. A recovery is ready only
            // when the dump attests actual final votes for both clubs.
            game.ActualOfficialVotesReady = !game.WasPostponed ? null : !game.Delayed &&
                actualClubs.Contains((game.SeasonIndex, game.SerieADay, game.HomeClubId)) && actualClubs.Contains((game.SeasonIndex, game.SerieADay, game.AwayClubId));
        }
    }
}

using Fantasoccer.StorageMigration;
using Fantasoccer.LegacyModels;

namespace Fantasoccer.DataMigration;

// Structural coordinates and dates only; never club labels or scores.
public sealed record ExcludedRealCalendarDocument(string DocumentSha256, string ArchiveReference, int SeasonIndex,
    int Days, int Games, int[] ContentSeasonIndexes, DateTime FirstKickoffLocal, DateTime LastKickoffLocal);

// A calendar whose own year was never stored (0) while every day carries the
// key's season: accepted by operator decision and reported.
public sealed record RealCalendarWithoutYearDocument(string DocumentSha256, string ArchiveReference, int SeasonIndex, int Days);

public sealed record DiscardedFutureScoreDocument(string DocumentSha256, string ArchiveReference, int SeasonIndex,
    int Games, DateTimeOffset ExportStartedAtUtc, string FirstJsonPath);

public sealed partial class CoreDatasetImporter
{
    // The legacy job saved the provider calendar under the app season, which
    // changes on 10 August: in July the next season could overwrite the
    // previous one. A calendar whose dated games all belong to another season
    // is preserved only in the dump. A mixed calendar is refused for review.
    private static ExcludedRealCalendarDocument? OtherSeasonCalendar(LegacyJsonDocument document, LegacyEntity<RealCalendar, int> calendar)
    {
        var days = calendar.Value?.Days ?? [];
        var dates = days.SelectMany(x => x.Games ?? []).Where(x => x.Date is not null).Select(x => Local(x.Date!.Value)).ToArray();
        var seasons = dates.Select(x => FantacalcioTime.SeasonYearAt(DateTime.SpecifyKind(x, DateTimeKind.Utc))).ToArray();
        var inside = seasons.Count(x => x == calendar.Key);
        if (inside == seasons.Length) return null;
        if (inside > 0) throw new CoreImportException("real_calendar_mixed_seasons", "calendar", "LegacyRealCalendars.cs", document.Sha256);
        return new(document.Sha256, DocumentArchiveReference(document), calendar.Key, days.Count, days.Sum(x => x.Games?.Count ?? 0),
            seasons.Distinct().Order().ToArray(), dates.Min(), dates.Max());
    }

    // Legacy calendars from 2023/24 on never stored their own year (0). The
    // key is accepted only when every day confirms it; any other difference
    // between the stored year and the key still stops the import.
    private static bool CalendarYearConfirmedByDays(RealCalendar calendar, int key)
        => calendar.Year == 0 && calendar.Days is { Count: > 0 } days && days.All(x => x is not null && x.Year == key);

    // A score cannot exist for a game that had not started when the export
    // began; legacy parsers stored placeholders. The game stays "to be played".
    private static bool FutureScore(RealGame game, DateTimeOffset exportStartedAtUtc)
        => (game.HomeGoal is not null || game.AwayGoal is not null) && game.Date is { } date && ToUtc(date) > exportStartedAtUtc;
}

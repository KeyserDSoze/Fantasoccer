namespace Fantasoccer.Application.Api.Mcp;

public sealed record FantasoccerSeason(
    int SeasonIndex, string Label, string FullLabel, int StartYear, int EndYear,
    DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtcExclusive,
    int FirstGlobalDayId, int LastGlobalDayId);

public sealed record FantasoccerSeasonSnapshot(
    DateTimeOffset ServerTimeUtc, FantasoccerSeason CurrentSeason,
    string SeasonRule, string YearParameterMeaning, string MatchdayRule,
    string AvailabilityRule);

public sealed class FantasoccerSeasonContext(TimeProvider time)
{
    public FantasoccerSeasonSnapshot GetCurrent()
    {
        var now = time.GetUtcNow();
        return new(now, Describe(FantacalcioTime.SeasonYearAt(now.UtcDateTime)),
            "Season 1 = 2012/13. The season changes on August 10 at 00:00 UTC. startYear = seasonIndex + 2011.",
            "API year and compact y are normally the 1-based season index, NOT a calendar year: use 15 for 2026/27, not 2026 or 2027.",
            "Serie A matchday = 1..38. A global day ID is (seasonIndex - 1) * 38 + matchday. Fantasy round number, Serie A matchday and global day ID are distinct: inspect the target model/operation.",
            "Current season is determined by server date, not by the user's saved selection or a live match. A group may not have this season configured; read its seasons from /api/groups/{groupId}. Use /api/football/{year}/calendar for actual scheduled/live matches.");
    }

    public static FantasoccerSeason Describe(int seasonIndex)
    {
        if (seasonIndex is < 1 or > 7987) throw new ArgumentOutOfRangeException(nameof(seasonIndex));
        var start = seasonIndex + 2011;
        return new(seasonIndex, $"{start}/{(start + 1) % 100:00}", FantacalcioTime.LabelYear(seasonIndex), start, start + 1,
            new(start, 8, 10, 0, 0, 0, TimeSpan.Zero), new(start + 1, 8, 10, 0, 0, 0, TimeSpan.Zero),
            FantacalcioTime.DayIdByDayYear(seasonIndex, 1), FantacalcioTime.DayIdByDayYear(seasonIndex, 38));
    }
}

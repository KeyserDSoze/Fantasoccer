using Fantasoccer.Application.Core.Groups;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Application.Core.Football;

public sealed partial class FootballIngestionApplicationService
{
    private static string? ProviderClubCode(RealTeam? club)
    {
        if (club?.Name is null) return null;
        try { return KeyUtils.GetClubKey(club.ExplicitAbbreviation, club.Name); }
        catch (ArgumentException) { return null; }
    }

    // Trusted job path, deliberately not an HTTP/MCP command. Historical years
    // require a pending waiting league and a provider that verifies that season.
    internal async Task RefreshPostponedDayAsync(int year, int day, CancellationToken cancellationToken)
    {
        var current = FantacalcioTime.SeasonYearAt(clock.GetUtcNow().UtcDateTime);
        if (year < 1 || year > current || day is < 1 or > 38)
            throw new ApplicationFault(409, "postponed_recovery_not_pending", "Contesto del recupero non valido.");
        var pendingMain = await database.FantasyMatches.AsNoTracking().AnyAsync(x => x.SeasonIndex == year && x.Matchday.SerieADay == day && x.Result == null &&
                database.LeagueRules.Any(rule => rule.GroupId == x.GroupId && rule.LeagueId == x.LeagueId && rule.SeasonIndex == year && rule.WaitForPostponedMatches) &&
                database.LeagueSeasons.Any(season => season.GroupId == x.GroupId && season.LeagueId == x.LeagueId && season.SeasonIndex == year && !season.IsArchived && !season.League.IsArchived), cancellationToken);
        var pendingOpening = await database.OpeningStandingSnapshots.AsNoTracking().AnyAsync(x => x.SeasonIndex == year && !x.IsComplete &&
                database.LeagueRules.Any(rule => rule.GroupId == x.GroupId && rule.LeagueId == x.LeagueId && rule.SeasonIndex == year && rule.WaitForPostponedMatches && rule.OpeningCompetitionEnabled && day <= rule.OpeningCompetitionSerieADays) &&
                database.LeagueSeasons.Any(season => season.GroupId == x.GroupId && season.LeagueId == x.LeagueId && season.SeasonIndex == year && !season.IsArchived && !season.League.IsArchived), cancellationToken);
        if (!pendingMain && !pendingOpening)
            throw new ApplicationFault(409, "postponed_recovery_not_pending", "Nessuna lega attiva attende questo recupero.");
        var matches = await database.FootballMatches.AsNoTracking().Where(x => x.SeasonIndex == year && x.SerieADay == day).ToArrayAsync(cancellationToken);
        var now = clock.GetUtcNow();
        bool Finished(global::Fantasoccer.Infrastructure.PostgreSql.Entities.FootballMatch game) => !game.Delayed &&
            (game.IsFinished == true || game.IsFinished is null && game.KickoffUtc is { } kickoff && now >= kickoff.AddMinutes(135));
        if (!pendingOpening && !matches.Any(x => x.WasPostponed || x.Delayed)) throw new ApplicationFault(409, "postponed_recovery_not_pending", "La giornata non contiene recuperi attestati.");
        if (matches.Any(x => (pendingOpening || x.WasPostponed || x.Delayed) && !Finished(x)))
        {
            RealCalendar parsed;
            try { parsed = await schedule.GetCalendarForSeasonAsync(year, day, day).WaitAsync(cancellationToken); }
            catch (NotSupportedException) { throw new ApplicationFault(409, "recovery_calendar_season_unavailable", "Il provider non può verificare il calendario di questa stagione; la giornata resta in attesa."); }
            var known = matches.Select(x => (x.HomeClubId, x.AwayClubId)).ToHashSet();
            if (parsed.Year != year || parsed.Days is not { Count: > 0 } || parsed.Days.Any(x => x.SerieADay != day) ||
                parsed.Days.SelectMany(x => x.Games).Any(x => ProviderClubCode(x.Home) is not { } home || ProviderClubCode(x.Away) is not { } away || !known.Contains((home, away))))
                throw new ApplicationFault(409, "recovery_calendar_scope_mismatch", "Il provider non ha confermato stagione, giornata e partite del recupero.");
            await StoreCalendarAsync(parsed, year, cancellationToken, null);
            database.ChangeTracker.Clear();
            matches = await database.FootballMatches.AsNoTracking().Where(x => x.SeasonIndex == year && x.SerieADay == day).ToArrayAsync(cancellationToken);
        }
        var liveClubs = matches.Where(x => (pendingOpening || x.WasPostponed || x.Delayed) && !x.Delayed && !Finished(x) && x.KickoffUtc is { } kickoff &&
                now >= kickoff && now <= kickoff.AddHours(10.25)).SelectMany(x => new[] { x.HomeClubId, x.AwayClubId }).ToHashSet();
        if (liveClubs.Count > 0) await LiveVotesAsync(year, day, cancellationToken, null, liveClubs);
        var recovered = matches.Where(x => (pendingOpening || x.WasPostponed || x.Delayed) && Finished(x) && x.ActualOfficialVotesReady != true)
            .SelectMany(x => new[] { x.HomeClubId, x.AwayClubId }).ToHashSet();
        if (recovered.Count > 0) await OfficialVotesAsync(year, day, cancellationToken, null, recovered);
    }
}

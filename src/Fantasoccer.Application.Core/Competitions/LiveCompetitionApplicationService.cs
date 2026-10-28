using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Fantasoccer.Application.Core.Caching;
using Fantasoccer.Application.Core.Football;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Application.Core.Competitions;

public sealed record LiveLeagueDto(Guid Id, string Name, Dictionary<string, CalendarDayDto> Rounds, FantasyRankDto? Rank, bool PreviewUnavailable);
public sealed record LiveGroupDto(string Name, int Year, LiveLeagueDto[] Leagues)
{
    public int? SerieADay { get; init; }
    public DateTimeOffset? GeneratedAtUtc { get; init; }
    // True while another instance rebuilds a projection whose sources changed.
    public bool IsStale { get; init; }
}

// Shared projection stored in the distributed cache: user-independent (no
// CanEdit flags, no personal data), one per group/season/day.
public sealed record LiveLeagueSnapshot(Guid Id, string Name, Dictionary<string, CalendarDayDto> Rounds, FantasyRankDto? Rank, bool PreviewUnavailable,
    Dictionary<Guid, GameDto> Games);
public sealed record LiveGroupSnapshot(Guid GroupId, string Name, int Year, int SerieADay, LiveLeagueSnapshot[] Leagues);

public sealed class LiveCompetitionApplicationService(FantasoccerDbContext database, CompetitionApplicationService competitions,
    GameApplicationService games, FootballApplicationService football, IFootballLiveDataStore liveData, SharedSnapshotCache cache,
    IAuthorizationService authorization, TimeProvider clock)
{
    public const string Kind = "live-group";
    private static readonly TimeSpan Retention = TimeSpan.FromDays(3);
    private static readonly TimeSpan MaxStale = TimeSpan.FromMinutes(2);

    public async Task<LiveGroupDto> GetAsync(ClaimsPrincipal caller, Guid group, int year, CancellationToken cancellationToken = default)
    {
        await AuthorizeAsync(caller, group);
        var day = await SelectedDayAsync(year, cancellationToken);
        var read = await SnapshotAsync(group, year, day ?? 0, cancellationToken);
        var snapshot = read.Envelope.Payload;
        return new(snapshot.Name, snapshot.Year, snapshot.Leagues.Select(x => new LiveLeagueDto(x.Id, x.Name, x.Rounds, x.Rank, x.PreviewUnavailable)).ToArray())
            { SerieADay = day, GeneratedAtUtc = read.Envelope.GeneratedAtUtc, IsStale = read.IsStale };
    }

    // View-mode game of the current live day, served from the shared projection.
    public async Task<GameDto?> TryGetGameAsync(ClaimsPrincipal caller, Guid group, Guid league, int year, Guid gameId, CancellationToken cancellationToken = default)
    {
        await AuthorizeAsync(caller, group);
        var day = await SelectedDayAsync(year, cancellationToken);
        if (day is null || !await database.FantasyMatches.AnyAsync(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year && x.Id == gameId &&
                x.Matchday.SerieADay == day, cancellationToken)) return null;
        var read = await SnapshotAsync(group, year, day.Value, cancellationToken);
        return read.Envelope.Payload.Leagues.SingleOrDefault(x => x.Id == league)?.Games.GetValueOrDefault(gameId);
    }

    internal async Task<CacheRead<LiveGroupSnapshot>> SnapshotAsync(Guid group, int year, int day, CancellationToken cancellationToken)
    {
        var stamp = await SourceStampAsync(group, year, day, cancellationToken);
        return await cache.GetOrRegenerateAsync(CacheKeys.LiveGroup(group, year, day), Kind, CacheKeys.LiveSchemaVersion,
            envelope => envelope.SourceFingerprint == stamp && envelope.Payload.GroupId == group,
            async token =>
            {
                var started = clock.GetUtcNow();
                var payload = await BuildAsync(group, year, day, token);
                return new CacheEnvelope<LiveGroupSnapshot>(CacheKeys.LiveSchemaVersion, Kind, started, started.AddSeconds(15), started.UtcTicks, stamp, payload);
            }, Retention, MaxStale, cancellationToken);
    }

    // Rebuilt only when a relevant source changed: votes, chances, lineups and
    // rosters of the day, results/calendars, rules/teams (group revision),
    // participations, real matches and the time phase of the day.
    internal async Task<string> SourceStampAsync(Guid group, int year, int day, CancellationToken cancellationToken)
    {
        // Deliberately not memoized: a handful of indexed aggregates per request keep
        // the shared projection exactly consistent with its sources.
        var builder = new StringBuilder();
        void Add(object? value) => builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append('|');
        Add(await database.Groups.Where(x => x.Id == group).Select(x => x.Revision).SingleOrDefaultAsync(cancellationToken));
        foreach (var calendar in await database.FantasyCalendars.Where(x => x.GroupId == group && x.SeasonIndex == year).OrderBy(x => x.LeagueId)
                     .Select(x => new { x.LeagueId, x.Version }).ToArrayAsync(cancellationToken)) Add($"{calendar.LeagueId:N}:{calendar.Version}");
        Add(await database.LeagueParticipations.CountAsync(x => x.GroupId == group && x.SeasonIndex == year, cancellationToken));
        var lineups = await database.LeagueLineups.Where(x => x.GroupId == group && x.SeasonIndex == year && x.Day == day)
            .GroupBy(_ => 1).Select(x => new { Count = x.Count(), Versions = x.Sum(l => l.Version), Last = x.Max(l => l.LastUpdateUtc) }).SingleOrDefaultAsync(cancellationToken);
        Add(lineups?.Count); Add(lineups?.Versions); Add(lineups?.Last.UtcTicks);
        var rosters = await database.RosterSnapshots.Where(x => x.GroupId == group && x.SeasonIndex == year && x.CompetitionId == Guid.Empty &&
                (x.Kind == RosterKind.Season || x.Kind == RosterKind.Matchday && x.Day == day))
            .GroupBy(x => x.Kind).Select(x => new { Kind = x.Key, Count = x.Count(), Versions = x.Sum(r => r.Version), Last = x.Max(r => r.LastUpdateUtc) })
            .OrderBy(x => x.Kind).ToArrayAsync(cancellationToken);
        foreach (var roster in rosters) Add($"{roster.Kind}:{roster.Count}:{roster.Versions}:{roster.Last?.UtcTicks}");
        var matches = await database.FootballMatches.Where(x => x.SeasonIndex == year && x.SerieADay == day).OrderBy(x => x.Ordinal)
            .Select(x => new { x.Ordinal, x.HomeGoals, x.AwayGoals, x.IsFinished, x.Delayed, x.KickoffUtc }).ToArrayAsync(cancellationToken);
        foreach (var match in matches) Add($"{match.Ordinal}:{match.HomeGoals}:{match.AwayGoals}:{match.IsFinished}:{match.Delayed}:{match.KickoffUtc?.UtcTicks}");
        var official = await database.FootballVotes.Where(x => x.SeasonIndex == year && x.Day == day && x.Source == FootballVoteSource.Official)
            .GroupBy(_ => 1).Select(x => new { Count = x.Count(), Sum = x.Sum(v => v.Value + v.Goals + v.Assists + v.Status) }).SingleOrDefaultAsync(cancellationToken);
        Add(official?.Count); Add(official?.Sum);
        Add((await liveData.LiveVotesAsync(year, day, cancellationToken))?.SourceRevision);
        Add((await liveData.ChancesAsync(year, day, cancellationToken))?.SourceRevision);
        // Before kickoff / live window / after: changes the roster used and the preview.
        var now = clock.GetUtcNow();
        var kickoffs = matches.Where(x => x.KickoffUtc != null).Select(x => x.KickoffUtc!.Value).ToArray();
        Add(kickoffs.Length == 0 ? "none" : now < kickoffs.Min() ? "before" : now <= kickoffs.Max().AddHours(4) ? "live" : "after");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private async Task<LiveGroupSnapshot> BuildAsync(Guid group, int year, int day, CancellationToken cancellationToken)
    {
        var name = await database.Groups.Where(x => x.Id == group).Select(x => x.Name).SingleAsync(cancellationToken);
        var leagues = await database.LeagueSeasons.AsNoTracking().Include(x => x.League).Where(x => x.GroupId == group && x.SeasonIndex == year && !x.IsArchived && !x.League.IsArchived)
            .OrderBy(x => x.League.Name).ToArrayAsync(cancellationToken);
        var result = new List<LiveLeagueSnapshot>();
        foreach (var league in leagues)
        {
            var fantasy = await competitions.CalendarCoreAsync(group, league.LeagueId, year, cancellationToken);
            var rounds = new Dictionary<string, CalendarDayDto>();
            var details = new Dictionary<Guid, GameDto>();
            var unavailable = false;
            foreach (var (stage, days) in fantasy?.Rounds ?? [])
                foreach (var calendarDay in days.Where(x => day > 0 && x.SerieADay == day))
                {
                    var previews = new List<CalendarGameDto>();
                    foreach (var game in calendarDay.Games)
                    {
                        try
                        {
                            var detail = await games.BuildAsync(league, game.Id, "view", null, false, cancellationToken);
                            details[game.Id] = detail;
                            previews.Add(detail.Game);
                        }
                        catch (ApplicationFault fault) when (fault.Code == "historical_roster_unavailable") { previews.Add(game); unavailable = true; }
                    }
                    rounds.Add(rounds.ContainsKey(stage) ? $"{stage}/turn/{calendarDay.Number}" : stage, calendarDay with { Games = previews.ToArray() });
                }
            // Definitive standings are read, never modified, by the projection.
            result.Add(new(league.LeagueId, league.League.Name, rounds, await competitions.CurrentRankAsync(group, league.LeagueId, year, cancellationToken), unavailable, details));
        }
        return new(group, name, year, day, result.ToArray());
    }

    private async Task<int?> SelectedDayAsync(int year, CancellationToken cancellationToken)
    {
        var calendar = await football.CalendarCoreAsync(year, cancellationToken);
        var now = clock.GetUtcNow();
        var windows = (calendar?.Days ?? []).Select(x => new { x.SerieADay, Times = x.Games.Where(game => !game.Delayed && !game.WasPostponed && game.Date != null).Select(game => game.Date!.Value).ToArray() })
            .Where(x => x.Times.Length > 0).ToArray();
        return windows.Where(x => now >= x.Times.Min() && now <= x.Times.Max().AddHours(10.25)).MaxBy(x => x.SerieADay)?.SerieADay
            ?? windows.Where(x => now < x.Times.Min()).MinBy(x => x.SerieADay)?.SerieADay
            ?? windows.MaxBy(x => x.SerieADay)?.SerieADay;
    }

    private async Task AuthorizeAsync(ClaimsPrincipal caller, Guid group)
    {
        // Every read of a group projection passes the application authorization.
        if (!(await authorization.AuthorizeAsync(caller, group, "workspace-member")).Succeeded)
            throw new ApplicationFault(403, "group_access_denied", "Accesso al gruppo non consentito.");
    }
}

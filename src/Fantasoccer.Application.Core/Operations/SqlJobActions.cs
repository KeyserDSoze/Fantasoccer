using Fantasoccer.Application.Core.Competitions;
using Fantasoccer.Application.Core.Football;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Application.Core.HallOfFame;
using Fantasoccer.Application.Core.Teams;
using Fantasoccer.Business.Services;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fantasoccer.Application.Core.Operations;

public sealed class SqlJobActions(IServiceScopeFactory scopes, TimeProvider clock, IOptions<JobMaintenanceOptions> options, ILogger<SqlJobActions> logger) : ISqlJobActions
{
    // Normal waits for provider data: reported as "waiting", never as a failure.
    private static readonly HashSet<string> WaitingFaults = ["official_matchday_incomplete", "competition_not_found"];

    private sealed class Skips
    {
        public int Count { get; private set; }
        public string? Code { get; private set; }
        public void Add(string? code) { Count++; Code ??= code; }
    }

    // One broken scope (a league, a day) must not stop the others: every scope runs in its own DI scope and
    // context, so its failure leaves nothing behind. A canceled job or a lost lease still stops everything.
    private string Failure(string job, Exception error, Guid group, Guid league, int day)
    {
        if (error is ApplicationFault fault) return fault.Code;
        logger.LogError("SQL job {JobName} skipped group {GroupId} league {LeagueId} day {Day} ({FailureType}); no exception message or payload logged.",
            job, group, league, day, error.GetType().Name);
        return "job_item_failed";
    }

    public async Task<SqlJobWorkResult> ExecuteAsync(string name, int year, CancellationToken cancellationToken)
    {
        // Job state/leases use their own context. Actions call the very same
        // application services as HTTP/MCP, with no repository fallback or fake user.
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider; var database = services.GetRequiredService<FantasoccerDbContext>();
        if (name == "formations") { await services.GetRequiredService<FormationAutomationService>().PrepareNextAsync(year, cancellationToken); return new(1, 0); }
        if (name == "images") { await services.GetRequiredService<IPlayerImageRefresher>().DownloadMissingAsync(cancellationToken); return new(1, 0); }
        if (name == "market-expiry") return await ExpireMarketAsync(database, year, cancellationToken);
        // Daily safety net: retention also runs when a new matchday is prepared, but
        // must not depend on a reminder actually being sent.
        if (name == "push-retention")
        {
            var cleaned = await services.GetRequiredService<Fantasoccer.Application.Core.Notifications.PushRetentionService>().CleanupAsync(year, cancellationToken);
            return new(cleaned.Deliveries + cleaned.FormationReceipts + cleaned.NotificationVotes, 0);
        }
        if (name == "cache-cleanup")
        {
            // Logical expiry is enforced on every read; this removes the physical blobs
            // (at most 5000 per run, but the whole namespace is always examined).
            var purged = await services.GetRequiredService<Fantasoccer.Caching.IDistributedCacheCoordinator>().PurgeExpiredAsync(TimeSpan.FromDays(1), 5000, cancellationToken);
            return new(purged.Deleted, 0);
        }
        if (name == "postponed-results") return await RecoverPostponedAsync(database, cancellationToken);
        if (name == "hall") return await RefreshHallBatchAsync(database, year, cancellationToken);
        var now = clock.GetUtcNow();
        var days = (await database.FootballMatches.AsNoTracking().Where(x => x.SeasonIndex == year && !x.Delayed && !x.WasPostponed && x.KickoffUtc != null).ToArrayAsync(cancellationToken))
            .GroupBy(x => x.SerieADay).Select(x => new { Day = x.Key, Start = x.Min(game => game.KickoffUtc!.Value), End = x.Max(game => game.KickoffUtc!.Value) }).ToArray();
        var live = days.Where(x => now >= x.Start && now <= x.End.AddHours(10.25)).MaxBy(x => x.Day);
        var next = days.Where(x => now < x.Start).MinBy(x => x.Day); var last = days.Where(x => now >= x.End.AddMinutes(135)).MaxBy(x => x.Day);
        if (name == "results")
        {
            var ended = (await database.FootballMatches.AsNoTracking().Where(x => x.SeasonIndex == year && (x.OriginalKickoffUtc ?? x.KickoffUtc) != null).ToArrayAsync(cancellationToken))
                .GroupBy(x => x.SerieADay).Where(x => now >= x.Max(game => game.OriginalKickoffUtc ?? game.KickoffUtc)!.Value.AddMinutes(135)).Select(x => x.Key).ToArray();
            var pending = await database.FantasyMatches.AsNoTracking().Where(x => x.SeasonIndex == year && x.Result == null && ended.Contains(x.Matchday.SerieADay))
                .Select(x => new { x.GroupId, x.LeagueId, Day = x.Matchday.SerieADay }).Distinct()
                .OrderBy(x => x.GroupId).ThenBy(x => x.LeagueId).ThenBy(x => x.Day).Take(1000).ToArrayAsync(cancellationToken);
            var processed = 0; var skips = new Skips();
            foreach (var item in pending)
            {
                await using var child = scopes.CreateAsyncScope();
                try
                {
                    var result = await child.ServiceProvider.GetRequiredService<CompetitionLifecycleApplicationService>().RecalculateScheduledAsync(item.GroupId, item.LeagueId, year, item.Day, cancellationToken);
                    if (result.AwaitingRecovery) skips.Add(null); else processed++;
                }
                catch (Exception error) when (error is not (OperationCanceledException or SqlJobLeaseLostException))
                {
                    var code = Failure(name, error, item.GroupId, item.LeagueId, item.Day);
                    skips.Add(WaitingFaults.Contains(code) ? null : code);
                }
            }
            return new(processed, skips.Count, skips.Count > 0 ? skips.Code ?? "results_waiting_for_official_data" : null);
        }
        var ingestion = services.GetRequiredService<FootballIngestionApplicationService>();
        int? day = name switch { "live" => live?.Day, "official" => last?.Day, "chances" => (live ?? next)?.Day, _ => null };
        if (name is "live" or "official" or "chances" && day == null) return new(0, 0, "no_applicable_matchday");
        if (name == "statistics")
        {
            day = await database.FootballDatasets.Where(x => x.SeasonIndex == year && x.Kind == "official").Select(x => (int?)x.Day).MaxAsync(cancellationToken);
            if (day == null) return new(0, 0, "no_official_votes");
        }
        await ingestion.RunAsync(new(name, year, day), cancellationToken);
        return new(1, 0);
    }
    private async Task<SqlJobWorkResult> RecoverPostponedAsync(FantasoccerDbContext database, CancellationToken cancellationToken)
    {
        // Do not limit this query to the latest Serie A day or current season.
        // Already closed default-policy days are never reopened automatically.
        var pending = await database.FantasyMatches.AsNoTracking().Where(x => x.Result == null &&
            database.LeagueRules.Any(rule => rule.GroupId == x.GroupId && rule.LeagueId == x.LeagueId && rule.SeasonIndex == x.SeasonIndex && rule.WaitForPostponedMatches) &&
            database.LeagueSeasons.Any(season => season.GroupId == x.GroupId && season.LeagueId == x.LeagueId && season.SeasonIndex == x.SeasonIndex && !season.IsArchived && !season.League.IsArchived) &&
            database.FootballMatches.Any(game => game.SeasonIndex == x.SeasonIndex && game.SerieADay == x.Matchday.SerieADay && (game.WasPostponed || game.Delayed)))
            .Select(x => new { x.GroupId, x.LeagueId, x.SeasonIndex, Day = x.Matchday.SerieADay,
                LastCheck = database.FootballMatchdays.Where(day => day.SeasonIndex == x.SeasonIndex && day.SerieADay == x.Matchday.SerieADay).Select(day => day.LastRecoveryCheckUtc).FirstOrDefault() })
            .Distinct().OrderBy(x => x.LastCheck).ThenBy(x => x.SeasonIndex).ThenBy(x => x.Day).Take(1000).ToArrayAsync(cancellationToken);
        var scopesToClose = pending.Select(x => new RecoveryScope(x.GroupId, x.LeagueId, x.SeasonIndex, x.Day, false, x.LastCheck)).ToList();
        var openings = await database.OpeningStandingSnapshots.AsNoTracking().Where(x => !x.IsComplete &&
            database.LeagueRules.Any(rule => rule.GroupId == x.GroupId && rule.LeagueId == x.LeagueId && rule.SeasonIndex == x.SeasonIndex && rule.WaitForPostponedMatches && rule.OpeningCompetitionEnabled) &&
            database.LeagueSeasons.Any(season => season.GroupId == x.GroupId && season.LeagueId == x.LeagueId && season.SeasonIndex == x.SeasonIndex && !season.IsArchived && !season.League.IsArchived))
            .Select(x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.SerieADays }).Take(1000).ToArrayAsync(cancellationToken);
        var openingYears = openings.Select(x => x.SeasonIndex).Distinct().ToArray();
        var openingDays = await database.FootballMatchdays.AsNoTracking().Where(x => openingYears.Contains(x.SeasonIndex))
            .Select(x => new { x.SeasonIndex, Day = x.SerieADay, LastCheck = x.LastRecoveryCheckUtc }).ToArrayAsync(cancellationToken);
        foreach (var opening in openings)
            scopesToClose.AddRange(openingDays.Where(day => day.SeasonIndex == opening.SeasonIndex && day.Day <= opening.SerieADays)
                .Select(day => new RecoveryScope(opening.GroupId, opening.LeagueId, opening.SeasonIndex, day.Day, true, day.LastCheck)));
        var processed = 0; var skipped = 0; string? code = null;
        foreach (var day in scopesToClose.OrderBy(x => x.LastCheck).ThenBy(x => x.SeasonIndex).ThenBy(x => x.Day).GroupBy(x => new { x.SeasonIndex, x.Day }).Take(25))
        {
            // Rotate bounded batches: an unsupported old season must not starve
            // newer recoveries on every scheduler run.
            await using (var stamp = scopes.CreateAsyncScope())
            {
                var context = stamp.ServiceProvider.GetRequiredService<FantasoccerDbContext>();
                await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(context, cancellationToken);
                (await context.FootballMatchdays.SingleAsync(x => x.SeasonIndex == day.Key.SeasonIndex && x.SerieADay == day.Key.Day, cancellationToken)).LastRecoveryCheckUtc = clock.GetUtcNow();
                await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
            }
            await using (var child = scopes.CreateAsyncScope())
            {
                try { await child.ServiceProvider.GetRequiredService<FootballIngestionApplicationService>().RefreshPostponedDayAsync(day.Key.SeasonIndex, day.Key.Day, cancellationToken); }
                catch (ApplicationFault fault) { code = fault.Code; }
                catch (HttpRequestException) { code = "recovery_source_unavailable"; }
                catch (Exception error) when (error is not (OperationCanceledException or SqlJobLeaseLostException))
                {
                    logger.LogError("SQL job postponed-results could not refresh season {SeasonIndex} day {Day} ({FailureType}); no exception message or payload logged.",
                        day.Key.SeasonIndex, day.Key.Day, error.GetType().Name);
                    code = "recovery_source_failed";
                }
            }
            foreach (var item in day)
            {
                await using var child = scopes.CreateAsyncScope();
                try
                {
                    if (item.Opening)
                    {
                        var opening = await child.ServiceProvider.GetRequiredService<OpeningCompetitionApplicationService>().PrepareScheduledAsync(item.GroupId, item.LeagueId, item.SeasonIndex, cancellationToken);
                        if (opening.IsComplete) processed++; else skipped++;
                        continue;
                    }
                    var result = await child.ServiceProvider.GetRequiredService<CompetitionLifecycleApplicationService>().RecalculateScheduledAsync(item.GroupId, item.LeagueId, item.SeasonIndex, item.Day, cancellationToken);
                    if (result.AwaitingRecovery) skipped++; else if (result.Changed) processed++;
                }
                catch (Exception error) when (error is not (OperationCanceledException or SqlJobLeaseLostException))
                { skipped++; code ??= Failure("postponed-results", error, item.GroupId, item.LeagueId, item.Day); }
            }
        }
        return new(processed, skipped, skipped > 0 ? code ?? "postponed_matchdays_pending" : null);
    }
    private sealed record RecoveryScope(Guid GroupId, Guid LeagueId, int SeasonIndex, int Day, bool Opening, DateTimeOffset? LastCheck);

    private async Task<SqlJobWorkResult> RefreshHallBatchAsync(FantasoccerDbContext database, int year, CancellationToken cancellationToken)
    {
        var cursor = await database.SqlJobStates.AsNoTracking().SingleOrDefaultAsync(x => x.Name == "hall" && x.SeasonIndex == year, cancellationToken);
        var active = database.Leagues.AsNoTracking().Where(x => !x.IsArchived);
        var pending = active;
        if (cursor?.CursorGroupId is { } group && cursor.CursorLeagueId is { } league)
            pending = pending.Where(x => x.GroupId.CompareTo(group) > 0 || x.GroupId == group && x.LeagueId.CompareTo(league) > 0);
        var limit = options.Value.HallBatchSize;
        var rows = await pending.OrderBy(x => x.GroupId).ThenBy(x => x.LeagueId).Select(x => new { x.GroupId, x.LeagueId }).Take(limit + 1).ToArrayAsync(cancellationToken);
        if (rows.Length == 0 && cursor?.CursorGroupId is not null)
            rows = await active.OrderBy(x => x.GroupId).ThenBy(x => x.LeagueId).Select(x => new { x.GroupId, x.LeagueId }).Take(limit + 1).ToArrayAsync(cancellationToken);
        var processed = 0; var skipped = 0;
        foreach (var row in rows.Take(limit))
        {
            // Persist progress even when this league needs reconciliation or a worker is interrupted. The next
            // run visits later keys, and a completed sweep wraps so failed/new leagues are revisited as well.
            await StoreHallCursorAsync(year, row.GroupId, row.LeagueId, cancellationToken);
            await using var child = scopes.CreateAsyncScope();
            try { await child.ServiceProvider.GetRequiredService<HallRefreshApplicationService>().RefreshScheduledAsync(row.GroupId, row.LeagueId, cancellationToken); processed++; }
            catch (ApplicationFault fault) when (fault.Code == "hall_baseline_reconciliation_required") { skipped++; }
        }
        var more = rows.Length > limit;
        if (!more) await StoreHallCursorAsync(year, null, null, cancellationToken);
        return new(processed, skipped, more ? "hall_batch_pending" : skipped > 0 ? "hall_baseline_reconciliation_required" : null) { HasMore = more };
    }

    private async Task StoreHallCursorAsync(int year, Guid? group, Guid? league, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<FantasoccerDbContext>();
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(context, cancellationToken);
        var state = await context.SqlJobStates.SingleOrDefaultAsync(x => x.Name == "hall" && x.SeasonIndex == year, cancellationToken);
        if (state is null)
        {
            if (!await context.Seasons.AnyAsync(x => x.SeasonIndex == year, cancellationToken)) context.Seasons.Add(new() { SeasonIndex = year, StartYear = year + 2011 });
            context.SqlJobStates.Add(state = new() { Name = "hall", SeasonIndex = year, NextRunAtUtc = clock.GetUtcNow() });
        }
        state.CursorGroupId = group; state.CursorLeagueId = league;
        await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
    }
    private async Task<SqlJobWorkResult> ExpireMarketAsync(FantasoccerDbContext database, int year, CancellationToken cancellationToken)
    {
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var cutoff = clock.GetUtcNow().AddDays(-14);
        var trades = await database.MarketTrades.Where(x => x.SeasonIndex == year && x.Status == 0 && x.CreatedAtUtc < cutoff).OrderBy(x => x.CreatedAtUtc).Take(500).ToArrayAsync(cancellationToken);
        foreach (var trade in trades)
        {
            trade.Status = 6; trade.Version++;
            database.MarketTradeEvents.Add(new() { GroupId = trade.GroupId, LeagueId = trade.LeagueId, SeasonIndex = year, TradeId = trade.Id,
                Kind = "expired", ResultingStatus = 6, TradeVersion = trade.Version, CreatedAtUtc = clock.GetUtcNow() });
        }
        await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        return new(trades.Length, 0);
    }
}

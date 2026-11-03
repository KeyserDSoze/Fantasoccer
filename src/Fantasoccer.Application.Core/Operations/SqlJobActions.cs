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
        if (name == "live-projections")
        {
            // Refresh even outside the live-votes window. Each group uses its own context.
            var groups = await database.LeagueSeasons.AsNoTracking()
                .Where(x => x.SeasonIndex == year && !x.IsArchived && !x.League.IsArchived)
                .Select(x => x.GroupId).Distinct().ToArrayAsync(cancellationToken);
            var processed = 0;
            var skipped = new Skips();
            foreach (var group in groups)
            {
                await using var groupScope = scopes.CreateAsyncScope();
                try
                {
                    await groupScope.ServiceProvider.GetRequiredService<LiveCompetitionApplicationService>()
                        .RefreshAsync(group, year, cancellationToken);
                    processed++;
                }
                catch (Exception error) when (error is not OperationCanceledException)
                { skipped.Add(Failure(name, error, group, Guid.Empty, 0)); }
            }
            return new(processed, skipped.Count, skipped.Code);
        }
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
        if (name == "job-retention")
        {
            var cutoff = clock.GetUtcNow().AddDays(-options.Value.RunRetentionDays);
            await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
            // Keep unfinished runs and any receipt still referenced by a lease,
            // even if it is old. Limit each transaction and continue in one minute.
            var ids = await database.SqlJobRuns.Where(x => x.Status != "Running" && x.CompletedAtUtc < cutoff &&
                    !database.SqlJobStates.Any(state => state.LeaseId == x.Id))
                .OrderBy(x => x.CompletedAtUtc).ThenBy(x => x.Id).Select(x => x.Id).Take(5000).ToArrayAsync(cancellationToken);
            var removed = await database.SqlJobRuns.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(cancellationToken);
            var failureIds = await database.OperationalFailures.Where(x => x.OccurredAtUtc < cutoff).OrderBy(x => x.OccurredAtUtc)
                .Select(x => x.Id).Take(5000).ToArrayAsync(cancellationToken);
            removed += await database.OperationalFailures.Where(x => failureIds.Contains(x.Id)).ExecuteDeleteAsync(cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(removed, 0) { HasMore = ids.Length == 5000 || failureIds.Length == 5000 };
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
        if (name == "official") return await OfficialAsync(database, ingestion, year, now, live?.Day, last?.Day, cancellationToken);
        int? day = name switch { "live" => live?.Day, "official" => last?.Day, "chances" => (live ?? next)?.Day, _ => null };
        if (name is "live" or "official" or "chances" && day == null) return new(0, 0, "no_applicable_matchday");
        if (name == "statistics")
        {
            day = await database.FootballDatasets.Where(x => x.SeasonIndex == year && x.Kind == "official").Select(x => (int?)x.Day).MaxAsync(cancellationToken);
            if (day == null) return new(0, 0, "no_official_votes");
        }
        try { await ingestion.RunAsync(new(name, year, day), cancellationToken); }
        catch (ApplicationFault fault) when (day is not null)
        {
            // After the last match the provider removes the live feed: not a failure, official votes take over.
            if (name == "live" && fault.Code == "football_provider_incomplete" && await MatchdayOverAsync(database, year, day.Value, now, cancellationToken))
                return new(0, 0, "live_votes_closed") { Detail = $"Giornata {day}: tutte le partite sono finite e il provider non pubblica più i voti live; restano gli ultimi salvati finché arrivano gli ufficiali." };
            // Probable lineups appear a day or two before the matchday: until then, an empty page is a wait (retried at the next slot), not an incident.
            if (name == "chances" && fault.Code == "chances_lineups_unavailable" && !await KickoffWithinAsync(database, year, day.Value, now, TimeSpan.FromHours(48), cancellationToken))
                return new(0, 1, "chances_lineups_pending") { Detail = fault.Message + " Mancano ancora più di 48 ore al calcio d'inizio: le probabili formazioni non sono ancora state pubblicate." };
            if (fault.Message.Contains("giornata", StringComparison.OrdinalIgnoreCase)) throw;
            throw new ApplicationFault(fault.Status, fault.Code, $"Giornata {day}: {fault.Message}");
        }
        return new(1, 0) { Detail = day is null ? null : $"Giornata {day}" };
    }
    // Matchday in progress: consolidate each finished match as its official votes appear. Matchday over: the full import (all clubs,
    // coaches, corrections) runs in the early-morning window, when the provider settles its votes, and any other time while matches
    // are still unconsolidated; if the full import cannot complete yet, the matches that are ready are consolidated one by one.
    private async Task<SqlJobWorkResult> OfficialAsync(FantasoccerDbContext database, FootballIngestionApplicationService ingestion, int year, DateTimeOffset now,
        int? liveDay, int? lastDay, CancellationToken cancellationToken)
    {
        if (liveDay is null && lastDay is null) return new(0, 0, "no_applicable_matchday");
        if (liveDay is { } running && (lastDay is null || running > lastDay))
            return Progress(running, await ingestion.OfficialProgressAsync(year, running, cancellationToken));
        var day = lastDay!.Value;
        var rome = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome"));
        var settling = rome.Hour is >= 3 and <= 10;
        // Outside the settling window only a matchday that ended a few hours ago keeps being polled, and only while something is missing.
        var recent = liveDay == lastDay;
        var unconsolidated = recent && await database.FootballMatches.AnyAsync(x => x.SeasonIndex == year && x.SerieADay == day && !x.Delayed && !x.WasPostponed && x.ActualOfficialVotesReady != true, cancellationToken);
        if (!settling && !unconsolidated) return new(0, 0, "official_votes_up_to_date") { Detail = $"Giornata {day}: voti ufficiali già consolidati." };
        try { await ingestion.RunAsync(new("official", year, day), cancellationToken); return new(1, 0) { Detail = $"Giornata {day}: voti ufficiali importati per tutte le partite." }; }
        catch (ApplicationFault fault) when (fault.Code is "official_matchday_incomplete" or "official_votes_not_published")
        {
            var progress = Progress(day, await ingestion.OfficialProgressAsync(year, day, cancellationToken));
            return progress with { Detail = progress.Detail + " " + fault.Message };
        }
    }
    private static async Task<bool> KickoffWithinAsync(FantasoccerDbContext database, int year, int day, DateTimeOffset now, TimeSpan window, CancellationToken cancellationToken)
    {
        var first = (await database.FootballMatches.AsNoTracking().Where(x => x.SeasonIndex == year && x.SerieADay == day && !x.Delayed && !x.WasPostponed && x.KickoffUtc != null)
            .Select(x => x.KickoffUtc).ToArrayAsync(cancellationToken)).Min();
        return first is null || first.Value - now <= window;
    }

    private static async Task<bool> MatchdayOverAsync(FantasoccerDbContext database, int year, int day, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var matches = await database.FootballMatches.AsNoTracking().Where(x => x.SeasonIndex == year && x.SerieADay == day && !x.Delayed && !x.WasPostponed).ToArrayAsync(cancellationToken);
        return matches.Length > 0 && matches.All(x => x.IsFinished == true || x.KickoffUtc is { } kickoff && now >= kickoff.AddMinutes(135));
    }

    private static SqlJobWorkResult Progress(int day, FootballIngestionApplicationService.OfficialProgress progress) => progress.Pending > 0
        ? new(progress.Imported, progress.Pending, "official_votes_pending")
            { Detail = $"Giornata {day}: {progress.Imported} partite consolidate ora; in attesa dei voti ufficiali: {string.Join(", ", progress.PendingMatches ?? [])}." }
        : new(progress.Imported, 0) { Detail = $"Giornata {day}: " + (progress.Imported > 0 ? $"{progress.Imported} partite consolidate con i voti ufficiali." : "nessuna partita finita da consolidare.") };

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
            .OrderBy(x => x.GroupId).ThenBy(x => x.LeagueId).ThenBy(x => x.SeasonIndex)
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
        var processed = 0; var skipped = 0; var reconcile = new List<(Guid GroupId, Guid LeagueId, string Reason)>(); var notes = new List<(Guid GroupId, Guid LeagueId, string Note)>();
        foreach (var row in rows.Take(limit))
        {
            // Persist progress even when this league needs reconciliation or a worker is interrupted. The next
            // run visits later keys, and a completed sweep wraps so failed/new leagues are revisited as well.
            await StoreHallCursorAsync(year, row.GroupId, row.LeagueId, cancellationToken);
            await using var child = scopes.CreateAsyncScope();
            try
            {
                var note = await child.ServiceProvider.GetRequiredService<HallRefreshApplicationService>().RefreshScheduledAsync(row.GroupId, row.LeagueId, cancellationToken);
                if (note != null) notes.Add((row.GroupId, row.LeagueId, note));
                processed++;
            }
            catch (ApplicationFault fault) when (fault.Code == "hall_baseline_reconciliation_required") { skipped++; reconcile.Add((row.GroupId, row.LeagueId, fault.Message)); }
        }
        string? detail = null;
        if (reconcile.Count + notes.Count > 0)
        {
            var groupIds = reconcile.Select(x => x.GroupId).Concat(notes.Select(x => x.GroupId)).Distinct().ToArray();
            var names = (await database.Leagues.AsNoTracking().Where(x => groupIds.Contains(x.GroupId)).Select(x => new { x.GroupId, x.LeagueId, x.Name }).ToArrayAsync(cancellationToken))
                .ToDictionary(x => (x.GroupId, x.LeagueId), x => x.Name);
            // The reason is ours (which owner, which totals): it says why the league cannot be reconciled by itself.
            var parts = notes.Select(x => $"{names.GetValueOrDefault((x.GroupId, x.LeagueId), "?")}: {x.Note}").ToList();
            if (reconcile.Count > 0)
                parts.Add($"{reconcile.Count} leghe da verificare per il refresh incrementale della Hall of Fame: " + string.Join(" | ", reconcile.Take(4).Select(x =>
                    $"{names.GetValueOrDefault((x.GroupId, x.LeagueId), "?")}: {(x.Reason.Length > 200 ? x.Reason[..200] + "…" : x.Reason)}")) + ". Ricontrollo ogni ora.");
            detail = string.Join(" ", parts);
        }
        var more = rows.Length > limit;
        if (!more) await StoreHallCursorAsync(year, null, null, cancellationToken);
        return new(processed, skipped, more ? "hall_batch_pending" : skipped > 0 ? "hall_baseline_reconciliation_required" : null) { HasMore = more, Detail = detail ?? $"{processed} leghe aggiornate." };
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

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

namespace Fantasoccer.Application.Core.Operations;

public sealed class SqlJobActions(IServiceScopeFactory scopes, TimeProvider clock) : ISqlJobActions
{
    public async Task<SqlJobWorkResult> ExecuteAsync(string name, int year, CancellationToken cancellationToken)
    {
        // Job state/leases use their own context. Actions call the very same
        // application services as HTTP/MCP, with no repository fallback or fake user.
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider; var database = services.GetRequiredService<FantasoccerDbContext>();
        if (name == "formations") { await services.GetRequiredService<FormationAutomationService>().PrepareNextAsync(year, cancellationToken); return new(1, 0); }
        if (name == "images") { await services.GetRequiredService<IPlayerImageRefresher>().DownloadMissingAsync(cancellationToken); return new(1, 0); }
        if (name == "market-expiry") return await ExpireMarketAsync(database, year, cancellationToken);
        if (name == "hall")
        {
            var leagues = await database.Leagues.AsNoTracking().Where(x => !x.IsArchived).Select(x => new { x.GroupId, x.LeagueId }).Take(1000).ToArrayAsync(cancellationToken);
            var processed = 0; var skipped = 0;
            foreach (var league in leagues)
            {
                // A refusal must not leave tracked deltas for the next league.
                await using var child = scopes.CreateAsyncScope();
                try { await child.ServiceProvider.GetRequiredService<HallRefreshApplicationService>().RefreshScheduledAsync(league.GroupId, league.LeagueId, cancellationToken); processed++; }
                catch (ApplicationFault fault) when (fault.Code == "hall_baseline_reconciliation_required") { skipped++; }
            }
            return new(processed, skipped, skipped > 0 ? "hall_baseline_reconciliation_required" : null);
        }
        var now = clock.GetUtcNow();
        var days = (await database.FootballMatches.AsNoTracking().Where(x => x.SeasonIndex == year && !x.Delayed && x.KickoffUtc != null).ToArrayAsync(cancellationToken))
            .GroupBy(x => x.SerieADay).Select(x => new { Day = x.Key, Start = x.Min(game => game.KickoffUtc!.Value), End = x.Max(game => game.KickoffUtc!.Value) }).ToArray();
        var live = days.Where(x => now >= x.Start && now <= x.End.AddHours(10.25)).MaxBy(x => x.Day);
        var next = days.Where(x => now < x.Start).MinBy(x => x.Day); var last = days.Where(x => now >= x.End.AddMinutes(135)).MaxBy(x => x.Day);
        if (name == "results")
        {
            var ended = days.Where(x => now >= x.End.AddMinutes(135)).Select(x => x.Day).ToArray();
            var pending = await database.FantasyMatches.AsNoTracking().Where(x => x.SeasonIndex == year && x.Result == null && ended.Contains(x.Matchday.SerieADay))
                .Select(x => new { x.GroupId, x.LeagueId, Day = x.Matchday.SerieADay }).Distinct().Take(1000).ToArrayAsync(cancellationToken);
            var processed = 0; var skipped = 0;
            foreach (var item in pending)
            {
                await using var child = scopes.CreateAsyncScope();
                try { await child.ServiceProvider.GetRequiredService<CompetitionLifecycleApplicationService>().RecalculateScheduledAsync(item.GroupId, item.LeagueId, year, item.Day, cancellationToken); processed++; }
                catch (ApplicationFault fault) when (fault.Code is "official_matchday_incomplete" or "competition_not_found") { skipped++; }
            }
            return new(processed, skipped, skipped > 0 ? "results_waiting_for_official_data" : null);
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

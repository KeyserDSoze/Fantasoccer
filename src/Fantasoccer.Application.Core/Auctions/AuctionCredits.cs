using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Application.Core.Auctions;

public sealed partial class AuctionApplicationService
{
    private async Task InitializeCreditsAsync(AuctionSession auction, FantasyTeamSeason[] teams, CancellationToken cancellationToken)
    {
        var rules = await database.LeagueRules.SingleAsync(x => x.GroupId == auction.GroupId && x.LeagueId == auction.LeagueId && x.SeasonIndex == auction.SeasonIndex, cancellationToken);
        var rank = await database.FantasyStandingSnapshots.AsNoTracking().Where(x => x.GroupId == auction.GroupId && x.LeagueId == auction.LeagueId && x.SeasonIndex == auction.SeasonIndex && x.SnapshotDay == 0)
            .AsSplitQuery().SingleOrDefaultAsync(cancellationToken);
        var opening = auction.Kind == 0 && rules.OpeningCompetitionEnabled
            ? await database.OpeningStandingSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.GroupId == auction.GroupId && x.LeagueId == auction.LeagueId && x.SeasonIndex == auction.SeasonIndex, cancellationToken) : null;
        if (auction.Kind == 0 && rules.OpeningCompetitionEnabled && opening is null)
            throw new ApplicationFault(409, "opening_not_prepared", "Preparare la competizione iniziale prima di creare l'asta.");
        foreach (var team in teams)
        {
            var roster = await RosterAsync(auction, team.Id, cancellationToken);
            var changed = false;
            if (rank != null && (rules.MoneyForGoal != 0 || rules.MoneyForSufferedGoal != 0))
            {
                var row = rank.Stages.OrderBy(x => x.Ordinal).SelectMany(x => x.Teams.OrderBy(teamRow => teamRow.Ordinal)).FirstOrDefault(x => x.TeamId == team.Id);
                var entitlement = row is null ? 0L : (long)row.GoalsFor * rules.MoneyForGoal + (long)row.GoalsAgainst * rules.MoneyForSufferedGoal;
                if (roster.RankCreditGranted is not { } granted)
                    throw new ApplicationFault(409, "rank_credit_reconciliation_required", "Il saldo storico non registra i premi classifica già accreditati. Un responsabile deve confermare il checkpoint tramite api/teams/credits/confirm-rank-checkpoint; nessun credito è stato ricalcolato.");
                var money = (long)roster.MoneyFromRank + entitlement - granted;
                if (money is < int.MinValue or > int.MaxValue || entitlement is < int.MinValue or > int.MaxValue) throw Invalid("Premio classifica fuori dai limiti supportati.");
                if (granted != entitlement) { roster.MoneyFromRank = (int)money; roster.RankCreditGranted = (int)entitlement; changed = true; }
            }
            if (opening != null)
            {
                var owner = team.Owners.Single(x => x.IsPrimary).UserId;
                var previous = await database.TeamOwnerships.Where(x => x.GroupId == auction.GroupId && x.BasketId == team.BasketId && x.SeasonIndex == auction.SeasonIndex - 1 && x.IsPrimary && x.UserId == owner)
                    .Select(x => x.TeamId).ToArrayAsync(cancellationToken);
                var rows = opening.Teams.Where(x => previous.Contains(x.TeamId) && x.BasketId == team.BasketId).ToArray();
                if (rows.Length > 1) throw Invalid("Premio iniziale non risolvibile in una sola squadra precedente.");
                var prize = rows.SingleOrDefault()?.Prize ?? 0;
                if (prize != roster.OpeningCompetitionPrize) { roster.OpeningCompetitionPrize = prize; changed = true; }
            }
            if (changed) { roster.Version++; roster.LastUpdateUtc = clock.GetUtcNow(); }
        }
        await availability.SynchronizeFutureAsync(auction.GroupId, auction.SeasonIndex, teams.Select(x => x.Id).ToArray(), cancellationToken);
    }
}

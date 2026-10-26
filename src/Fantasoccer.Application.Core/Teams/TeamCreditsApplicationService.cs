using System.Security.Claims;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Application.Core.Teams;

public sealed record ConfirmRankCreditsRequest(Guid GroupId, Guid BasketId, int Year, Guid TeamId, int ExpectedVersion, int ExpectedMoneyFromRank, int GrantedRankCredits);
public sealed class TeamCreditsApplicationService(FantasoccerDbContext database, TimeProvider clock)
{
    public async Task ConfirmAsync(ClaimsPrincipal caller, ConfirmRankCreditsRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var email = caller.FindFirstValue(ClaimTypes.Email)?.Trim().ToLowerInvariant();
        var actor = await database.GroupMemberships.Where(x => x.GroupId == request.GroupId && x.User.Email == email && x.User.IsEnabled && (x.Roles & GroupRole.SuperAdmin) != 0)
            .Select(x => (Guid?)x.UserId).SingleOrDefaultAsync(cancellationToken) ?? throw new ApplicationFault(403, "credits_manager_required", "Serve un responsabile del gruppo per confermare i crediti storici.");
        var row = await database.RosterSnapshots.SingleOrDefaultAsync(x => x.GroupId == request.GroupId && x.BasketId == request.BasketId && x.SeasonIndex == request.Year && x.TeamId == request.TeamId && x.Kind == RosterKind.Season, cancellationToken)
            ?? throw new ApplicationFault(404, "roster_not_found", "Rosa non trovata.");
        if (row.Version != request.ExpectedVersion || row.MoneyFromRank != request.ExpectedMoneyFromRank)
            throw new ApplicationFault(409, "roster_changed", "Ricaricare la rosa e il saldo prima di confermare il checkpoint.");
        database.RankCreditConfirmations.Add(new() { GroupId = request.GroupId, BasketId = request.BasketId, SeasonIndex = request.Year, TeamId = request.TeamId,
            ActorUserId = actor, PreviousGranted = row.RankCreditGranted, Granted = request.GrantedRankCredits, BalanceAtConfirmation = row.MoneyFromRank, RecordedAtUtc = clock.GetUtcNow() });
        row.RankCreditGranted = request.GrantedRankCredits;
        row.Version++;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}

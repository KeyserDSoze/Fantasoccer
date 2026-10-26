using System.Security.Claims;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Application.Core.Teams;

public sealed record SynchronizeRosterAvailabilityRequest(Guid GroupId, Guid BasketId, int Year, Guid TeamId);
public sealed class TeamRosterAvailabilityService(FantasoccerDbContext database, TimeProvider clock)
{
    public async Task SynchronizeAsync(ClaimsPrincipal caller, SynchronizeRosterAvailabilityRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var email = caller.FindFirstValue(ClaimTypes.Email)?.Trim().ToLowerInvariant();
        var member = await database.GroupMemberships.Where(x => x.GroupId == request.GroupId && x.User.Email == email && x.User.IsEnabled && x.Roles != GroupRole.None)
            .Select(x => new { x.UserId, x.Roles }).SingleOrDefaultAsync(cancellationToken) ?? throw Denied();
        var team = await database.TeamSeasons.AsNoTracking().Include(x => x.Owners).SingleOrDefaultAsync(x => x.GroupId == request.GroupId && x.BasketId == request.BasketId &&
            x.SeasonIndex == request.Year && x.Id == request.TeamId, cancellationToken) ?? throw new ApplicationFault(404, "team_not_found", "Squadra non trovata nel contesto richiesto.");
        var owned = team.Owners.Any(owner => owner.UserId == member.UserId);
        if (!owned && !member.Roles.HasFlag(GroupRole.SuperAdmin)) throw Denied();
        await SynchronizeFutureAsync(request.GroupId, request.Year, [request.TeamId], cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    // Called only inside the mutating service's transaction. Transfers, repairs
    // and removals must not leave an upcoming lineup with players no longer owned.
    public async Task SynchronizeFutureAsync(Guid group, int year, Guid[] teamIds, CancellationToken cancellationToken)
    {
        if (database.Database.CurrentTransaction is null) throw new InvalidOperationException("Roster availability synchronization requires the caller's transaction.");
        var days = await database.FootballMatches.Where(x => x.SeasonIndex == year && !x.Delayed && x.KickoffUtc != null).GroupBy(x => x.SerieADay)
            .Where(x => x.Min(game => game.KickoffUtc) > clock.GetUtcNow()).Select(x => x.Key).ToArrayAsync(cancellationToken);
        if (days.Length == 0) return;
        var current = await database.RosterSnapshots.Include(x => x.Players).ThenInclude(x => x.Player).Include(x => x.Players).ThenInclude(x => x.Club)
            .Where(x => x.GroupId == group && x.SeasonIndex == year && x.Kind == RosterKind.Season && teamIds.Contains(x.TeamId)).AsSplitQuery().ToArrayAsync(cancellationToken);
        var future = await database.RosterSnapshots.Include(x => x.Players).ThenInclude(x => x.Player).Include(x => x.Players).ThenInclude(x => x.Club)
            .Include(x => x.AdditionalOwners).ThenInclude(x => x.User).Where(x => x.GroupId == group && x.SeasonIndex == year && x.Kind == RosterKind.Matchday &&
                teamIds.Contains(x.TeamId) && days.Contains(x.Day)).AsSplitQuery().ToArrayAsync(cancellationToken);
        foreach (var row in future)
        {
            var source = current.Single(x => x.TeamId == row.TeamId);
            var before = row.Players.OrderBy(x => x.Ordinal).Select(Signature).ToArray();
            var after = source.Players.OrderBy(x => x.Ordinal).Select(Signature).ToArray();
            if (before.SequenceEqual(after) && row.MoneyFromRank == source.MoneyFromRank && row.OpeningCompetitionPrize == source.OpeningCompetitionPrize &&
                row.MarketBalanceAdjustment == source.MarketBalanceAdjustment && row.RankCreditGranted == source.RankCreditGranted) continue;
            var positions = row.Players.Where(x => x.Status == 0).ToDictionary(x => x.PlayerId, x => x.Position);
            var activeIds = source.Players.Where(x => x.Status == 0).Select(x => x.PlayerId).ToHashSet();
            var outgoingSlots = row.Players.Where(x => x.Status == 0 && !activeIds.Contains(x.PlayerId)).OrderBy(x => x.Ordinal)
                .GroupBy(x => x.PlayerRole).ToDictionary(x => x.Key, x => new Queue<int>(x.Select(player => player.Position)));
            var players = source.Players.OrderBy(x => x.Ordinal).Select(x =>
            {
                var position = x.Position;
                if (x.Status == 0)
                {
                    if (positions.TryGetValue(x.PlayerId, out var existing)) position = existing;
                    else if (outgoingSlots.TryGetValue(x.PlayerRole, out var slots) && slots.TryDequeue(out var outgoing)) position = outgoing;
                }
                return new RosterPlayer { Ordinal = x.Ordinal, PlayerId = x.PlayerId, Player = x.Player, ClubId = x.ClubId, Club = x.Club,
                    PlayerRole = x.PlayerRole, IsActive = x.IsActive, IsVisible = x.IsVisible, Price = x.Price, Revenue = x.Revenue, Status = x.Status, Position = position };
            }).ToArray();
            row.Players.Clear();
            await database.SaveChangesAsync(cancellationToken);
            foreach (var player in players) row.Players.Add(player);
            row.MoneyFromRank = source.MoneyFromRank; row.OpeningCompetitionPrize = source.OpeningCompetitionPrize;
            row.MarketBalanceAdjustment = source.MarketBalanceAdjustment; row.RankCreditGranted = source.RankCreditGranted;
            row.Version++; row.LastUpdateUtc = clock.GetUtcNow();
        }
    }
    private static (string, int, string, int, bool, bool, int, int, int) Signature(RosterPlayer player) =>
        (player.PlayerId, player.Ordinal, player.ClubId, player.PlayerRole, player.IsActive, player.IsVisible, player.Price, player.Revenue, player.Status);
    private static ApplicationFault Denied() => new(403, "roster_access_denied", "Non sei autorizzato ad aggiornare la disponibilità di questa rosa.");
}

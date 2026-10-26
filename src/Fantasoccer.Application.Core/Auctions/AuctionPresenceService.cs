using System.Security.Claims;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Application.Core.Auctions;

public sealed partial class AuctionApplicationService
{
    public async Task JoinAsync(ClaimsPrincipal caller, Guid id, string connectionId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var auction = await SessionAsync(id, true, cancellationToken);
        var actor = await ActorAsync(caller, auction.GroupId, cancellationToken);
        var row = await database.AuctionPresence.SingleOrDefaultAsync(x => x.AuctionId == id && x.ConnectionId == connectionId, cancellationToken);
        if (row is null) database.AuctionPresence.Add(new() { GroupId = auction.GroupId, LeagueId = auction.LeagueId, SeasonIndex = auction.SeasonIndex,
            AuctionId = id, ConnectionId = connectionId, UserId = actor.Id, ConnectedAtUtc = clock.GetUtcNow() });
        else if (row.UserId != actor.Id) throw Denied();
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
    public async Task LeaveAsync(ClaimsPrincipal caller, Guid id, string connectionId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var email = caller.FindFirstValue(ClaimTypes.Email)?.Trim().ToLowerInvariant();
        await database.AuctionPresence.Where(x => x.AuctionId == id && x.ConnectionId == connectionId && database.Users.Any(user => user.Id == x.UserId && user.Email == email)).ExecuteDeleteAsync(cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
    public async Task DisconnectAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var removed = await database.AuctionPresence.Where(x => x.ConnectionId == connectionId).ExecuteDeleteAsync(cancellationToken);
        if (removed > 0) { await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
    }
    public async Task<string[]> ConnectedUsersAsync(ClaimsPrincipal caller, Guid id, CancellationToken cancellationToken = default)
    {
        var auction = await SessionAsync(id, false, cancellationToken);
        await ActorAsync(caller, auction.GroupId, cancellationToken);
        return await ConnectedUsersCoreAsync(auction, cancellationToken);
    }
    public async Task AuthorizeViewAsync(ClaimsPrincipal caller, Guid id, CancellationToken cancellationToken = default)
    {
        var auction = await SessionAsync(id, false, cancellationToken);
        await ActorAsync(caller, auction.GroupId, cancellationToken);
    }
    private Task<string[]> ConnectedUsersCoreAsync(AuctionSession auction, CancellationToken cancellationToken) =>
        database.AuctionPresence.Where(x => x.AuctionId == auction.Id && database.GroupMemberships.Any(member => member.GroupId == x.GroupId && member.UserId == x.UserId && member.Roles != GroupRole.None && member.User.IsEnabled && member.User.Email != null))
            .Select(x => database.Users.Where(user => user.Id == x.UserId).Select(user => user.Email!).First()).Distinct().Order().ToArrayAsync(cancellationToken);
}

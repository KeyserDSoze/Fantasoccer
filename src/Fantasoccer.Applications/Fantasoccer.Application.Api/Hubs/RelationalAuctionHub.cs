using System.Security.Claims;
using Fantasoccer.Application.Core.Auctions;
using Fantasoccer.Application.Core.Groups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Fantasoccer.Application.Api.Hubs;

[Authorize(Policy = "authenticated")]
public sealed class RelationalAuctionHub(AuctionApplicationService auctions) : Hub
{
    public async Task JoinAsync(Guid auctionId)
    {
        await auctions.JoinAsync(Context.User!, auctionId, Context.ConnectionId, Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, Room(auctionId), Context.ConnectionAborted);
        await Clients.Group(Room(auctionId)).SendAsync("PresenceChanged", auctionId, Context.ConnectionAborted);
    }
    public async Task LeaveAsync(Guid auctionId)
    {
        await auctions.LeaveAsync(Context.User!, auctionId, Context.ConnectionId, Context.ConnectionAborted);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, Room(auctionId), Context.ConnectionAborted);
        await Clients.Group(Room(auctionId)).SendAsync("PresenceChanged", auctionId, Context.ConnectionAborted);
    }
    public Task<string[]> ConnectedUsersAsync(Guid auctionId) => auctions.ConnectedUsersAsync(Context.User!, auctionId, Context.ConnectionAborted);
    public async Task<AuctionUpdateDto> CommandAsync(Guid auctionId, string operation, AuctionCommandRequest request)
    {
        try { return await auctions.CommandAsync(Context.User!, auctionId, operation, request, Context.ConnectionAborted); }
        catch (ApplicationFault fault) { throw new HubException($"{fault.Code}: {fault.Message}"); }
    }
    public async Task EmojiAsync(Guid auctionId, string emoji)
    {
        await auctions.AuthorizeViewAsync(Context.User!, auctionId, Context.ConnectionAborted);
        if (string.IsNullOrWhiteSpace(emoji) || emoji.Length > 16) throw new HubException("Invalid emoji.");
        await Clients.Group(Room(auctionId)).SendAsync("EmojiReceived", new { emoji, sender = Context.User!.FindFirstValue(ClaimTypes.Email) }, Context.ConnectionAborted);
    }
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await auctions.DisconnectAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
    internal static string Room(Guid id) => "sql-auction:" + id.ToString("N");
}

public sealed class SignalRAuctionUpdatePublisher(IHubContext<RelationalAuctionHub> hub, ILogger<SignalRAuctionUpdatePublisher> logger) : IAuctionUpdatePublisher
{
    public async Task PublishAsync(AuctionUpdateDto update, CancellationToken cancellationToken)
    {
        try { await hub.Clients.Group(RelationalAuctionHub.Room(update.AuctionId)).SendAsync("AuctionUpdated", update, cancellationToken); }
        catch (Exception error) { logger.LogWarning(error, "Auction {AuctionId} committed version {Version}; clients must refresh after a missed update.", update.AuctionId, update.Version); }
    }
}

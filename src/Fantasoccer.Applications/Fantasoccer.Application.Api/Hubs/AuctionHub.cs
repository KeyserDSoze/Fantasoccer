using Microsoft.AspNetCore.SignalR;

namespace Fantasoccer.Application.Api.Hubs
{
    public sealed class AuctionHub : Hub
    {
        public async Task RiseUpAsync(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }
    }
}

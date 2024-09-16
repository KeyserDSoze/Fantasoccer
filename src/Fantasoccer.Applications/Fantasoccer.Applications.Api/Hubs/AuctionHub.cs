using Microsoft.AspNetCore.SignalR;

namespace Fantasoccer.Applications.Api.Hubs
{
    public sealed class AuctionHub : Hub
    {
        public async Task RiseUpAsync(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }
    }
}

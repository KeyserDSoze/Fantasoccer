using Microsoft.AspNetCore.SignalR.Client;

namespace Fantasoccer.Applications.App.ClientServices
{
    public sealed class AuctionHubClientSettings
    {
        public string ApiUri { get; init; }
    }

    public class AuctionHubClient : IAsyncDisposable
    {
        private readonly AuctionHubClientSettings _settings;
        public AuctionHubClient(AuctionHubClientSettings settings)
        {
            _settings = settings;
        }
        private HubConnection? _hubConnection;
        public async ValueTask StartAsync(Action<string, string> onReceive)
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(_settings.ApiUri)
                .Build();

            _hubConnection.On<string, string>("ReceiveMessage", (user, message) =>
            {
                onReceive(user, message);
            });

            await _hubConnection.StartAsync();
        }
        public async ValueTask SendAsync(string user, string message)
        {
            await _hubConnection.SendAsync("RiseUpAsync", user, message);
        }
        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
        public async ValueTask DisposeAsync()
        {
            if (_hubConnection is not null)
            {
                await _hubConnection.DisposeAsync();
            }
        }
    }
}

using FantacalcioV4.Database;
using Rystem.Background;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Timers;

namespace FantacalcioV4.Engine.IngestionData
{
    public class PlayerImagesJob : IBackgroundJob
    {
        private readonly IRepository<FantacalcioLog, string> _logger;
        private readonly HttpClient HttpClient;
        public PlayerImagesJob( IRepository<FantacalcioLog, string> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            HttpClient = httpClientFactory.CreateClient("base");
        }

        public Task ActionToDoAsync()
            => DownloadPlayerImages.InvokeAsync(FantacalcioTime.ActualYear, HttpClient);

        public Task OnException(Exception exception)
            => Logger.CatchAsync<PlayerImagesJob>(exception);
    }
}
using Fantasoccer.Engine.IngestionData;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;
using Rystem.Content;

namespace Fantasoccer.Tests.Tests.BackgroundJobs
{
    public class PlayerImagesTest
    {
        private readonly IFantasoccerLogger _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IRepository<RealPlayersWrapper, int> _realPlayers;
        private readonly IFactory<IContentRepository> _factory;

        public PlayerImagesTest(IFantasoccerLogger logger, IHttpClientFactory httpClientFactory,
            IRepository<RealPlayersWrapper, int> realPlayers,
            IFactory<IContentRepository> factory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _realPlayers = realPlayers;
            _factory = factory;
        }
        [Fact]
        public async Task CheckAsync()
        {
            var playerImagesJob = new PlayerImagesJob(_logger, _httpClientFactory, _realPlayers, _factory);
            await playerImagesJob.ActionToDoAsync(default);
        }
    }
}

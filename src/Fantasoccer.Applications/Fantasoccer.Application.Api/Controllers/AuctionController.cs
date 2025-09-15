using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepositoryFramework;

namespace Fantasoccer.Application.Api.Controllers
{
    [AllowAnonymous]
    [ApiController]
    [Route("[controller]")]
    public class AuctionController : ControllerBase
    {
        private IRepository<RealPlayersWrapper, int> RealPlayers { get; }
        private IRepository<RealPlayersWrapper, LeagueKey> RandomizedRealPlayers { get; }
        public AuctionController(IRepository<RealPlayersWrapper, int> realPlayers, IRepository<RealPlayersWrapper, LeagueKey> randomizedRealPlayers)
        {
            RealPlayers = realPlayers;
            RandomizedRealPlayers = randomizedRealPlayers;
        }

        [HttpGet]
        public async Task<RealPlayer?> GetNextPlayerAsync([FromQuery] string group, [FromQuery] string league, [FromQuery] int year, [FromQuery] bool isRandom = false)
        {
            LeagueKey leagueKey = new LeagueKey
            {
                Group = group,
                League = league,
                Year = year,
            };
            var allPlayersWrapper = isRandom ?
                await RandomizedRealPlayers.GetAsync(leagueKey) :
                await RealPlayers.GetAsync(leagueKey.Year);
            var playersFromStore = isRandom == true ?
                    allPlayersWrapper.Players.OrderBy(x => x.Role).ToList() :
                    allPlayersWrapper.Players.OrderBy(x => x.Role).ThenBy(x => x.Name).ToList();
            var players = playersFromStore.Where(x => x.IsActive).ToList();
            return await ShowTheNextPlayerAsync(leagueKey, players);

        }
        private async Task<RealPlayer?> ShowTheNextPlayerAsync(LeagueKey leagueKey, List<RealPlayer> players)
        {
            foreach (var player in players.OrderBy(x => x.Role))
            {
                if (!player.Visible)
                {
                    player.Visible = true;
                    await RandomizedRealPlayers.UpdateAsync(leagueKey, new RealPlayersWrapper { Players = players });
                    return player;
                }
            }
            return default;
        }
    }
}

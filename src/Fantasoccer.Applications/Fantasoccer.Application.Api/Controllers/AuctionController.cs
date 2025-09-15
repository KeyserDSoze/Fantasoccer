using System;
using System.Drawing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepositoryFramework;

namespace Fantasoccer.Application.Api.Controllers
{
    [AllowAnonymous]
    [ApiController]
    [Route("[controller]/[action]")]
    public class AuctionController : ControllerBase
    {
        private IRepository<RealPlayersWrapper, int> RealPlayers { get; }
        private IRepository<RealPlayersWrapper, LeagueKey> RandomizedRealPlayers { get; }
        private IRepository<Team, TeamKey> Team { get; }
        private IRepository<Group, string> Groups { get; }
        public AuctionController(IRepository<RealPlayersWrapper, int> realPlayers, IRepository<RealPlayersWrapper, LeagueKey> randomizedRealPlayers, IRepository<Team, TeamKey> team, IRepository<Group, string> groups)
        {
            RealPlayers = realPlayers;
            RandomizedRealPlayers = randomizedRealPlayers;
            Team = team;
            Groups = groups;
        }
        [HttpGet]
        public async Task<string> GetTeamNameAsync([FromQuery] string email, [FromQuery] string group, [FromQuery] string basket, [FromQuery] int year)
        {
            var teamKey = new TeamKey { Year = year, Email = email, Group = group, BasketId = basket };
            var team = await Team.GetAsync(teamKey);
            if (team == null)
            {
                var theGroup = await Groups.GetAsync(group);
                var theBasket = theGroup.Baskets.First(x => x.Id == basket);
                var theYear = theBasket.Years.First(x => x.Year == year);
                var theTeam = theYear.Teams.First(x => x.Owner == email);
                team = new() { Name = theTeam.Name, Owner = theTeam.Owner, Players = [] };
                await Team.UpdateAsync(teamKey, team);
            }
            return team.Name;
        }
        [HttpGet]
        public async ValueTask<bool> SetPlayerAsync([FromQuery] string email, [FromQuery] string group, [FromQuery] string league, [FromQuery] string basket, [FromQuery] int year, [FromQuery] string playerName, [FromQuery] int price, [FromQuery] bool isRandom = false)
        {
            var teamKey = new TeamKey { Year = year, Email = email, Group = group, BasketId = basket };
            var team = await Team.GetAsync(teamKey);
            var leagueKey = new LeagueKey
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
            var thePlayer = players.FirstOrDefault(x => x.Name == playerName);
            team.Players.Add(new Player
            {
                IsActive = true,
                Name = thePlayer.Name,
                RealTeam = thePlayer.RealTeam,
                Price = price,
                Role = thePlayer.Role,
                Status = PlayerInTeamStatus.Active,
                Position = FantaSoccerRole.Tribune
            });
            return await Team.UpdateAsync(teamKey, team);
        }

        [HttpGet]
        public async Task<RealPlayer?> GetNextPlayerAsync([FromQuery] string group, [FromQuery] string league, [FromQuery] int year, [FromQuery] int role, [FromQuery] bool isRandom = false)
        {
            var theRole = (Role)role;
            var leagueKey = new LeagueKey
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
            return await ShowTheNextPlayerAsync(leagueKey, players, theRole);

        }
        private async Task<RealPlayer?> ShowTheNextPlayerAsync(LeagueKey leagueKey, List<RealPlayer> players, Role role)
        {
            foreach (var player in players.Where(x => x.Role == role).OrderBy(x => x.Role))
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

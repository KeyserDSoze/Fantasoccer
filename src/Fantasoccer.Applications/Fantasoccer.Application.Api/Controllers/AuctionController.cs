using System;
using System.Drawing;
using System.Text.Json.Serialization;
using Fantasoccer.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
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
        private IRepository<StatPlayersWrapper, int> StatsPlayers { get; }
        public AuctionController(IRepository<RealPlayersWrapper, int> realPlayers, IRepository<RealPlayersWrapper, LeagueKey> randomizedRealPlayers, IRepository<Team, TeamKey> team, IRepository<Group, string> groups, IRepository<StatPlayersWrapper, int> statsPlayers)
        {
            RealPlayers = realPlayers;
            RandomizedRealPlayers = randomizedRealPlayers;
            Team = team;
            Groups = groups;
            StatsPlayers = statsPlayers;
        }
        [HttpGet]
        public async Task<Group> GetGroupAsync([FromQuery] string groupId)
        {
            var group = await Groups.GetAsync(groupId);
            return group;
        }
        [HttpGet]
        public async Task<List<StatPlayer>> GetAllPlayersAsync([FromQuery] int year)
        {
            var statPlayers = await StatsPlayers.GetAsync(year);
            if (statPlayers != null)
                return statPlayers.Players;
            else
                return [];
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
        public sealed class ShowingTeam
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }
            [JsonPropertyName("owner")]
            public string Owner { get; set; }
            [JsonPropertyName("cost")]
            public int Cost { get; set; }
            [JsonPropertyName("players")]
            public List<Player> Players { get; set; }
        }
        [HttpGet]
        public async IAsyncEnumerable<ShowingTeam> GetTeamsAsync([FromQuery] string group, [FromQuery] string basket, [FromQuery] int year)
        {
            var theGroup = await Groups.GetAsync(group);
            var theBasket = theGroup.Baskets.First(x => x.Id == basket);
            var theYear = theBasket.Years.First(x => x.Year == year);
            foreach (var teamInYear in theYear.Teams)
            {
                var teamKey = new TeamKey
                {
                    Group = group,
                    BasketId = basket,
                    Year = year,
                    Email = teamInYear.Owner
                };
                var team = await Team.GetAsync(teamKey);
                if (team == null)
                {
                    team = new() { Name = teamInYear.Name, Owner = teamInYear.Owner, Players = [] };
                    await Team.UpdateAsync(teamKey, team);
                }
                var showingTeam = new ShowingTeam
                {
                    Name = team.Name,
                    Owner = team.Owner,
                    Cost = team.Cost,
                    Players = team.ActivePlayers.ToList()
                };
                yield return showingTeam;
            }
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
            var check = Check(thePlayer, team, price);
            if (string.IsNullOrWhiteSpace(check))
            {
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
            else
            {
                throw new Exception(check);
            }
        }
        private string Check(RealPlayer realPlayer, Team team, int price)
        {
            switch (realPlayer.Role)
            {
                case Role.GoalKeeper:
                    if (team.ActivePlayers.Count(x => x.Role == realPlayer.Role) >= 3)
                    {
                        return "Troppi portieri";
                    }
                    break;
                case Role.Defensor:
                    if (team.ActivePlayers.Count(x => x.Role == realPlayer.Role) >= 8)
                    {
                        return "Troppi difensori";
                    }
                    break;
                case Role.Midfielder:
                    if (team.ActivePlayers.Count(x => x.Role == realPlayer.Role) >= 8)
                    {
                        return "Troppi centrocampisti";
                    }
                    break;
                case Role.Forward:
                    if (team.ActivePlayers.Count(x => x.Role == realPlayer.Role) >= 6)
                    {
                        return "Troppi attaccanti";
                    }
                    break;
            }
            if (1000 - (team.Cost + price + (25 - 1 - team.ActivePlayers.Count())) < 0)
            {
                return "Stai spendendo troppi soldi";
            }
            return string.Empty;
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

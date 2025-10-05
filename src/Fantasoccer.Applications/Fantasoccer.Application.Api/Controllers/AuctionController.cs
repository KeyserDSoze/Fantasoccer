using System.IO;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Fantasoccer.Domain;
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
        private readonly IRepository<AuctionWrapper, string> _auctionRepository;
        private readonly IRepository<AppIdentity, string> _identityRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private readonly IRepository<RealPlayersWrapper, int> _realPlayers;
        private IRepository<RealPlayersWrapper, LeagueKey> RandomizedRealPlayers { get; }
        private readonly IRepository<Team, TeamKey> _team;
        private IRepository<Group, string> Groups { get; }
        private IRepository<StatPlayersWrapper, int> StatsPlayers { get; }
        public AuctionController(IRepository<AuctionWrapper, string> auctionRepository,
            IRepository<AppIdentity, string> identityRepository,
            IHttpContextAccessor httpContextAccessor,
            IRepository<RealPlayersWrapper, int> realPlayers, IRepository<RealPlayersWrapper, LeagueKey> randomizedRealPlayers, IRepository<Team, TeamKey> team, IRepository<Group, string> groups, IRepository<StatPlayersWrapper, int> statsPlayers)
        {
            _auctionRepository = auctionRepository;
            _identityRepository = identityRepository;
            _httpContextAccessor = httpContextAccessor;
            _realPlayers = realPlayers;
            RandomizedRealPlayers = randomizedRealPlayers;
            _team = team;
            Groups = groups;
            StatsPlayers = statsPlayers;
        }
        [HttpGet]
        public async Task<string> CreateAuctionAsync([FromQuery] AuctionType auctionType)
        {
            var name = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
            if (name != null)
            {
                var identity = await _identityRepository.GetAsync(name);
                if (identity?.State?.GroupId != null && identity?.State?.LeagueId != null)
                {
                    var group = await Groups.GetAsync(identity.State.GroupId);
                    if (group != null)
                    {
                        //if the league is main of the group
                        if (group.Leagues.Any(x => x.Id == identity.State.LeagueId && x.IsMain))
                        {
                            //if the user is admin or superadmin of the group
                            if (group.Participants.Any(x => x.Email == identity.Email && (x.Role.HasFlag(IdentityRole.Admin) || x.Role.HasFlag(IdentityRole.SuperAdmin))))
                            {
                                var auction = new AuctionWrapper
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    Creator = identity.Email,
                                    Created = DateTime.UtcNow,
                                    Type = auctionType,
                                    LeagueKey = new LeagueKey
                                    {
                                        Group = group.Id,
                                        League = identity.State.LeagueId,
                                        Year = FantacalcioTime.ActualYear,
                                    },
                                    ListOfPlayers = await GetListOfPlayers(auctionType, group, identity.State.LeagueId),
                                    LastShow = new()
                                    {
                                        { Role.GoalKeeper, null },
                                        { Role.Defensor, null },
                                        { Role.Midfielder, null },
                                        { Role.Forward, null }
                                    }
                                };
                                await _auctionRepository.InsertAsync(auction.Id, auction);
                                return auction.Id;
                            }
                        }
                    }
                }
            }
            return string.Empty;
        }
        private async Task<Dictionary<Role, List<string>>> GetListOfPlayers(AuctionType auctionType, Group group, string leagueKey)
        {
            Dictionary<Role, List<string>> players = [];
            var league = group.GetAnnualLeague(leagueKey, FantacalcioTime.ActualYear);
            var leagueWrapper = group.Leagues.FirstOrDefault(x => x.Id == leagueKey);
            if (league != null && leagueWrapper != null)
            {
                var teams = new List<Team>();
                foreach (var basketId in leagueWrapper.BasketsId)
                {
                    var basket = group.Baskets.First(x => x.Id == basketId);
                    var theYear = basket.Years.First(x => x.Year == FantacalcioTime.ActualYear);
                    foreach (var teamInYear in theYear.Teams)
                    {
                        var teamKey = new TeamKey
                        {
                            Group = group.Id,
                            BasketId = basketId,
                            Year = FantacalcioTime.ActualYear,
                            Email = teamInYear.Owner
                        };
                        var team = _team.GetAsync(teamKey).ToResult();
                        if (team == null)
                        {
                            team = new() { Name = teamInYear.Name, Owner = teamInYear.Owner, Players = [] };
                            _team.UpdateAsync(teamKey, team).ToResult();
                        }
                        teams.Add(team);
                    }
                }
                var allPlayers = await _realPlayers.GetAsync(FantacalcioTime.ActualYear);
                if (auctionType == AuctionType.Normal)
                {
                    players = allPlayers.Players
                        .Where(x => x.Role != Role.Undefined)
                        .GroupBy(x => x.Role)
                        .ToDictionary(x => x.Key, x => x.Select(t => t.Key).Where(x => !teams.SelectMany(t => t.Players).Any(q => q.Key == x)).ToList());
                }
                else if (auctionType == AuctionType.RandomByLetter)
                {
                    //devo ordinare i giocatore in base a un carattere iniziale random, faccio un esempio se esce C, prendo tutti i giocatori che iniziano per C, poi D, E, e così via fino a Z e poi riparto da A e finisco con B
                    //inoltre devo escludere i giocatori che sono già stati presi dalle squadre
                    //inoltre devo scegliere un carattere iniziale random per ogni ruolo
                    var random = new Random();
                    var letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
                    foreach (var role in Enum.GetValues<Role>().Where(x => x != Role.Undefined))
                    {
                        var listForRole = allPlayers.Players
                            .Where(x => x.Role == role && !teams.SelectMany(t => t.Players).Any(q => q.Key == x.Key))
                            .OrderBy(x => x.Name)
                            .ToList();
                        var startIndex = random.Next(0, letters.Length);
                        var orderedList = new List<string>();
                        for (var i = 0; i < letters.Length; i++)
                        {
                            var letter = letters[(startIndex + i) % letters.Length];
                            orderedList.AddRange(listForRole.Where(x => x.Name.StartsWith(letter.ToString(), StringComparison.InvariantCultureIgnoreCase)).Select(x => x.Key));
                        }
                        players.Add(role, orderedList);
                    }
                }
                else if (auctionType == AuctionType.RandomList)
                {
                    //devo prendere la lista randomizzata dei giocatori
                    //inoltre devo escludere i giocatori che sono già stati presi dalle squadre
                    //devo prendere i giocatori in ordine di ruolo
                    //crea come sopra il metodo che randomizza tutti i giocatori
                    var randomizedPlayers = allPlayers.Players
                        .OrderBy(x => x.Role)
                        .ThenBy(x => RandomNumberGenerator.GetInt32(0, int.MaxValue))
                        .ToList();
                    players = randomizedPlayers.GroupBy(x => x.Role)
                        .ToDictionary(x => x.Key, x => x.Select(t => t.Key).Where(x => !teams.SelectMany(t => t.Players).Any(q => q.Key == x)).ToList());
                }
            }
            return players;
        }

        [HttpGet]
        public async Task<string> GetTeamNameAsync([FromQuery] string email, [FromQuery] string group, [FromQuery] string basket, [FromQuery] int year)
        {
            var teamKey = new TeamKey { Year = year, Email = email, Group = group, BasketId = basket };
            var team = await _team.GetAsync(teamKey);
            if (team == null)
            {
                var theGroup = await Groups.GetAsync(group);
                var theBasket = theGroup.Baskets.First(x => x.Id == basket);
                var theYear = theBasket.Years.First(x => x.Year == year);
                var theTeam = theYear.Teams.First(x => x.Owner == email);
                team = new() { Name = theTeam.Name, Owner = theTeam.Owner, Players = [] };
                await _team.UpdateAsync(teamKey, team);
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
                var team = await _team.GetAsync(teamKey);
                if (team == null)
                {
                    team = new() { Name = teamInYear.Name, Owner = teamInYear.Owner, Players = [] };
                    await _team.UpdateAsync(teamKey, team);
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


    }
}

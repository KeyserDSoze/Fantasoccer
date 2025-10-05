using System.Text.Json;
using Fantasoccer.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using RepositoryFramework;

namespace Fantasoccer.Application.Api.Hubs
{
    public sealed class AuctionHub : Hub
    {
        private sealed class AuctionHubWrapper
        {
            public AuctionWrapper Wrapper { get; set; }
            public DateTime LastUpdate { get; set; }
            public Group? Group { get; set; }
            public AnnualLeague? League { get; set; }
            public string? LeagueName { get; set; }
            public Dictionary<TeamKey, Team> Teams { get; set; } = [];
            public List<StatPlayer>? RealPlayers { get; set; }
            public string LastJsonSaved { get; set; }
        }
        private AuctionHubWrapper? GetCurrentWrapper(string auctionId)
        {
            var added = false;
            if (!_auctions.ContainsKey(auctionId))
            {
                lock (s_lock)
                {
                    if (!_auctions.ContainsKey(auctionId))
                    {
                        var auction = _auctionRepository.GetAsync(auctionId).GetAwaiter().GetResult();
                        if (auction == null)
                            throw new InvalidOperationException("Auction not found");
                        var group = _groups.GetAsync(auction.LeagueKey.Group).ToResult();
                        if (group != null)
                        {
                            var league = group.GetAnnualLeague(auction.LeagueKey.League, auction.LeagueKey.Year);
                            var leagueWrapper = group.Leagues.FirstOrDefault(x => x.Id == auction.LeagueKey.League);
                            if (league != null && leagueWrapper != null)
                            {
                                var statPlayers = _statsPlayers.GetAsync(auction.LeagueKey.Year).ToResult();
                                var teams = new Dictionary<TeamKey, Team>();
                                foreach (var basketId in leagueWrapper.BasketsId)
                                {
                                    var basket = group.Baskets.First(x => x.Id == basketId);
                                    var theYear = basket.Years.First(x => x.Year == auction.LeagueKey.Year);
                                    foreach (var teamInYear in theYear.Teams)
                                    {
                                        var teamKey = new TeamKey
                                        {
                                            Group = auction.LeagueKey.Group,
                                            BasketId = basketId,
                                            Year = auction.LeagueKey.Year,
                                            Email = teamInYear.Owner
                                        };
                                        var team = _team.GetAsync(teamKey).ToResult();
                                        if (team == null)
                                        {
                                            team = new() { Name = teamInYear.Name, Owner = teamInYear.Owner, Players = [] };
                                            _team.UpdateAsync(teamKey, team).ToResult();
                                        }
                                        teams.Add(teamKey, team);
                                    }
                                }
                                added = _auctions.TryAdd(auctionId, new AuctionHubWrapper
                                {
                                    Wrapper = auction,
                                    LastUpdate = DateTime.UtcNow,
                                    Group = group,
                                    League = league,
                                    LeagueName = leagueWrapper?.Name ?? auction.LeagueKey.League,
                                    RealPlayers = statPlayers?.Players ?? [],
                                    Teams = teams,
                                    LastJsonSaved = auction.ToJson()
                                });
                            }
                        }
                    }
                }
            }
            _auctions.TryGetValue(auctionId, out var wrapper);
            if (added && wrapper != null)
            {
                _ = UpdateAsync(auctionId);
            }
            return wrapper;
        }
        private async ValueTask UpdateAsync(string auctionId)
        {
            await Task.Delay(TimeSpan.FromSeconds(10));
            if (_auctions.TryGetValue(auctionId, out var wrapper))
            {
                var json = wrapper.Wrapper.ToJson();
                if (json != wrapper.LastJsonSaved)
                {
                    wrapper.LastJsonSaved = json;
                    await _auctionRepository.UpdateAsync(auctionId, wrapper.Wrapper);
                }
                if (DateTime.UtcNow.AddDays(-2) > wrapper.Wrapper.Created)
                {
                    wrapper.Wrapper.Status = AuctionStatus.Finished;
                    await _auctionRepository.UpdateAsync(auctionId, wrapper.Wrapper);
                }
                if (wrapper.Wrapper.Status != AuctionStatus.Finished)
                {
                    _ = UpdateAsync(auctionId);
                }
            }
        }
        private readonly IRepository<AuctionWrapper, string> _auctionRepository;
        private readonly IRepository<AppIdentity, string> _identityRepository;
        private readonly IRepository<RealPlayersWrapper, int> _realPlayers;
        private readonly IRepository<RealPlayersWrapper, LeagueKey> _randomizedRealPlayers;
        private readonly IRepository<Team, TeamKey> _team;
        private readonly IRepository<Group, string> _groups;
        private readonly IRepository<StatPlayersWrapper, int> _statsPlayers;
        private static readonly object s_lock = new();
        private readonly Dictionary<string, AuctionHubWrapper> _auctions = new();
        public AuctionHub(IRepository<AuctionWrapper, string> auctionRepository,
            IRepository<AppIdentity, string> identityRepository,
            IRepository<RealPlayersWrapper, int> realPlayers, IRepository<RealPlayersWrapper, LeagueKey> randomizedRealPlayers, IRepository<Team, TeamKey> team, IRepository<Group, string> groups, IRepository<StatPlayersWrapper, int> statsPlayers)
        {
            _auctionRepository = auctionRepository;
            _identityRepository = identityRepository;
            _realPlayers = realPlayers;
            _randomizedRealPlayers = randomizedRealPlayers;
            _team = team;
            _groups = groups;
            _statsPlayers = statsPlayers;
        }
        public async Task RiseUpAsync(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }
        private async Task<(bool Correct, string Message)> SetPlayerAsync(string auctionId, string owner, string playerName, int price)
        {
            var auctionWrapper = GetCurrentWrapper(auctionId);
            var thePlayer = auctionWrapper.RealPlayers.FirstOrDefault(x => x.Name == playerName);
            var team = auctionWrapper.Teams.FirstOrDefault(x => x.Key.Email == owner);
            var check = Check(thePlayer, team.Value, price);
            if (string.IsNullOrWhiteSpace(check))
            {
                team.Value.Players.Add(new Player
                {
                    IsActive = true,
                    Name = thePlayer.Name,
                    RealTeam = thePlayer.RealTeam,
                    Price = price,
                    Role = thePlayer.Role,
                    Status = PlayerInTeamStatus.Active,
                    Position = FantaSoccerRole.Tribune
                });
                return (await _team.UpdateAsync(team.Key, team.Value), string.Empty);
            }
            else
            {
                return (false, check);
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
        private async Task<RealPlayer?> GetNextPlayerAsync(string auctionId, Role role)
        {
            var auctionWrapper = GetCurrentWrapper(auctionId);
            var lastNameFound = auctionWrapper.Wrapper.LastShow[role];
            var foundPrevious = false;
            foreach (var player in auctionWrapper.Wrapper.ListOfPlayers[role])
            {
                if (foundPrevious)
                    return auctionWrapper.RealPlayers.First(x => x.Name == player);
                if (player == lastNameFound)
                {
                    foundPrevious = true;
                }
            }
            return null;
        }
        private async Task<RealPlayer?> GetLastPlayerAsync(string auctionId, Role role)
        {
            var auctionWrapper = GetCurrentWrapper(auctionId);
            var lastNameFound = auctionWrapper.Wrapper.LastShow[role];
            if (lastNameFound != null)
                return auctionWrapper.RealPlayers.First(x => x.Name == lastNameFound);
            else
                return null;
        }
    }
}

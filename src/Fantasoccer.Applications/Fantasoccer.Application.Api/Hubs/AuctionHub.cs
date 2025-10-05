using System.Text.Json;
using System.Text.Json.Serialization;
using Fantasoccer.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using RepositoryFramework;

namespace Fantasoccer.Application.Api.Hubs
{
    [Authorize(Policy = "authenticated")]
    public sealed class AuctionHub : Hub
    {
        private sealed class AuctionHubWrapper
        {
            [JsonPropertyName("w")]
            public required AuctionWrapper Wrapper { get; set; }
            [JsonPropertyName("u")]
            public DateTime LastUpdate { get; set; }
            [JsonPropertyName("g")]
            public Group? Group { get; set; }
            [JsonPropertyName("l")]
            public AnnualLeague? League { get; set; }
            [JsonPropertyName("ln")]
            public string? LeagueName { get; set; }
            [JsonPropertyName("t")]
            public Dictionary<TeamKey, Team> Teams { get; set; } = [];
            [JsonPropertyName("r")]
            public List<StatPlayer>? RealPlayers { get; set; }
            [JsonIgnore]
            public required string LastJsonSaved { get; set; }
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
        private async Task<(bool Correct, string Message)> SetPlayerAsync(string auctionId, string owner, string playerKey, int price)
        {
            var auctionWrapper = GetCurrentWrapper(auctionId);
            if (auctionWrapper?.RealPlayers == null)
                return (false, "Auction data not available");

            var thePlayer = auctionWrapper.RealPlayers.FirstOrDefault(x => x.Key == playerKey);
            if (thePlayer == null)
                return (false, "Player not found");

            var team = auctionWrapper.Teams.FirstOrDefault(x => x.Key.Email == owner);
            if (team.Value == null)
                return (false, "Team not found");

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
        private Task<RealPlayer?> GetNextPlayerAsync(string auctionId, Role role)
        {
            var auctionWrapper = GetCurrentWrapper(auctionId);
            if (auctionWrapper?.Wrapper?.LastShow == null || auctionWrapper.RealPlayers == null)
                return Task.FromResult<RealPlayer?>(null);

            var lastNameFound = auctionWrapper.Wrapper.LastShow[role];
            var foundPrevious = false;
            foreach (var player in auctionWrapper.Wrapper.ListOfPlayers[role])
            {
                if (foundPrevious)
                    return Task.FromResult<RealPlayer?>(auctionWrapper.RealPlayers.First(x => x.Key == player));
                if (player == lastNameFound)
                {
                    foundPrevious = true;
                }
            }
            return Task.FromResult<RealPlayer?>(null);
        }

        // ============================================
        // HELPER METHODS
        // ============================================

        /// <summary>
        /// Get user email from authentication context
        /// </summary>
        private string? GetUserEmail()
        {
            return Context.User?.Identity?.Name;
        }

        /// <summary>
        /// Check if user is auctioneer (admin or super admin of the group)
        /// </summary>
        private bool IsAuctioneer(AuctionHubWrapper wrapper, string userEmail)
        {
            if (wrapper.Group == null) return false;

            var participant = wrapper.Group.Participants.FirstOrDefault(x => x.Email == userEmail);
            if (participant == null) return false;

            return participant.Role.HasFlag(IdentityRole.Admin) ||
                   participant.Role.HasFlag(IdentityRole.SuperAdmin);
        }

        /// <summary>
        /// Check if user is a participant (has a team in this auction)
        /// </summary>
        private bool IsParticipant(AuctionHubWrapper wrapper, string userEmail)
        {
            return wrapper.Teams.Any(x => x.Key.Email == userEmail);
        }

        /// <summary>
        /// Check if user can access this auction (either auctioneer or participant)
        /// </summary>
        private bool CanAccessAuction(AuctionHubWrapper wrapper, string userEmail)
        {
            return IsAuctioneer(wrapper, userEmail) || IsParticipant(wrapper, userEmail);
        }

        // ============================================
        // SIGNALR HUB METHODS (PUBLIC API)
        // ============================================

        /// <summary>
        /// Join an auction room - first method to call after connection
        /// </summary>
        public async Task<bool> JoinAuctionAsync(string auctionId)
        {
            var email = GetUserEmail();
            if (string.IsNullOrEmpty(email))
            {
                await Clients.Caller.SendAsync("Error", "User not authenticated");
                return false;
            }

            try
            {
                var wrapper = GetCurrentWrapper(auctionId);
                if (wrapper == null)
                {
                    await Clients.Caller.SendAsync("Error", "Auction not found");
                    return false;
                }

                if (!CanAccessAuction(wrapper, email))
                {
                    await Clients.Caller.SendAsync("Error", "You don't have access to this auction");
                    return false;
                }

                // Add user to SignalR group
                await Groups.AddToGroupAsync(Context.ConnectionId, auctionId);

                // Add to participants if not already there
                if (!wrapper.Wrapper.Participants.Any(x => x.Owner == email))
                {
                    var team = wrapper.Teams.FirstOrDefault(x => x.Key.Email == email);
                    if (team.Value != null)
                    {
                        wrapper.Wrapper.Participants.Add(new AnnualTeam
                        {
                            Name = team.Value.Name,
                            Owner = email,
                            AdditionalOwners = []
                        });
                    }
                }

                // Notify others
                await Clients.OthersInGroup(auctionId).SendAsync("UserJoined", email);

                // Send current state to caller
                await SendAuctionStateAsync(auctionId);

                return true;
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", $"Failed to join auction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Leave auction room
        /// </summary>
        public async Task LeaveAuctionAsync(string auctionId)
        {
            var email = GetUserEmail();
            if (string.IsNullOrEmpty(email)) return;

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, auctionId);
            await Clients.OthersInGroup(auctionId).SendAsync("UserLeft", email);
        }

        /// <summary>
        /// Get current auction state (teams, players, current player, etc.)
        /// </summary>
        public async Task SendAuctionStateAsync(string auctionId)
        {
            var email = GetUserEmail();
            if (string.IsNullOrEmpty(email)) return;

            var wrapper = GetCurrentWrapper(auctionId);
            if (wrapper == null || !CanAccessAuction(wrapper, email)) return;

            await Clients.Caller.SendAsync("AuctionState", wrapper);
        }

        /// <summary>
        /// AUCTIONEER: Show next player for bidding
        /// </summary>
        public async Task ShowNextPlayerAsync(string auctionId, Role role)
        {
            var email = GetUserEmail();
            if (string.IsNullOrEmpty(email)) return;

            var wrapper = GetCurrentWrapper(auctionId);
            if (wrapper == null || !IsAuctioneer(wrapper, email))
            {
                await Clients.Caller.SendAsync("Error", "Only auctioneers can show next player");
                return;
            }

            if (wrapper.Wrapper.Status == AuctionStatus.Finished)
            {
                await Clients.Caller.SendAsync("Error", "Auction is already finished");
                return;
            }

            var nextPlayer = await GetNextPlayerAsync(auctionId, role);
            if (nextPlayer == null)
            {
                await Clients.Group(auctionId).SendAsync("Error", $"No more players for role {role}");
                return;
            }

            // Update auction state
            wrapper.Wrapper.Actual = new ActualAuction
            {
                Player = wrapper.RealPlayers.First(x => x.Key == nextPlayer.Key),
                Owner = null,
                Price = 0,
                Started = DateTime.UtcNow
            };
            wrapper.Wrapper.LastShow[role] = nextPlayer.Key;
            wrapper.Wrapper.Status = AuctionStatus.InProgress;

            // Broadcast to all participants
            await Clients.Group(auctionId).SendAsync("PlayerShown", wrapper.Wrapper.Actual);
        }

        /// <summary>
        /// AUCTIONEER: Assign current player to a team
        /// </summary>
        public async Task AssignPlayerToTeamAsync(string auctionId, string ownerEmail, int price)
        {
            var email = GetUserEmail();
            if (string.IsNullOrEmpty(email)) return;

            var wrapper = GetCurrentWrapper(auctionId);
            if (wrapper == null || !IsAuctioneer(wrapper, email))
            {
                await Clients.Caller.SendAsync("Error", "Only auctioneers can assign players");
                return;
            }

            if (wrapper.Wrapper.Actual == null)
            {
                await Clients.Caller.SendAsync("Error", "No player is currently being auctioned");
                return;
            }

            var (success, message) = await SetPlayerAsync(auctionId, ownerEmail, wrapper.Wrapper.Actual.Player.Key, price);

            if (!success)
            {
                await Clients.Caller.SendAsync("Error", message);
                return;
            }

            // Update winnings
            wrapper.Wrapper.Actual.Owner = ownerEmail;
            wrapper.Wrapper.Actual.Price = price;
            wrapper.Wrapper.Winnings.Add(new AuctionWin
            {
                PlayerKey = wrapper.Wrapper.Actual.Player.Key,
                Owner = wrapper.Wrapper.Actual.Owner,
                Price = wrapper.Wrapper.Actual.Price
            });
            wrapper.Wrapper.Actual = null;

            // Get updated team
            var team = wrapper.Teams.FirstOrDefault(x => x.Key.Email == ownerEmail).Value;

            // Broadcast to all participants
            await Clients.Group(auctionId).SendAsync("PlayerAssigned", new
            {
                Owner = ownerEmail,
                TeamName = team?.Name,
                Price = price,
                PlayerName = wrapper.RealPlayers.First(x => x.Key == wrapper.Wrapper.Winnings.Last().PlayerKey).Name,
                TeamCost = team?.Cost,
                TeamPlayersCount = team?.ActivePlayers.Count()
            });

            // Send updated state
            await Clients.Group(auctionId).SendAsync("AuctionUpdated");
        }

        /// <summary>
        /// AUCTIONEER: Pause auction
        /// </summary>
        public async Task PauseAuctionAsync(string auctionId)
        {
            var email = GetUserEmail();
            if (string.IsNullOrEmpty(email)) return;

            var wrapper = GetCurrentWrapper(auctionId);
            if (wrapper == null || !IsAuctioneer(wrapper, email))
            {
                await Clients.Caller.SendAsync("Error", "Only auctioneers can pause the auction");
                return;
            }

            wrapper.Wrapper.Status = AuctionStatus.Paused;
            await Clients.Group(auctionId).SendAsync("AuctionPaused");
        }

        /// <summary>
        /// AUCTIONEER: Resume auction
        /// </summary>
        public async Task ResumeAuctionAsync(string auctionId)
        {
            var email = GetUserEmail();
            if (string.IsNullOrEmpty(email)) return;

            var wrapper = GetCurrentWrapper(auctionId);
            if (wrapper == null || !IsAuctioneer(wrapper, email))
            {
                await Clients.Caller.SendAsync("Error", "Only auctioneers can resume the auction");
                return;
            }

            wrapper.Wrapper.Status = AuctionStatus.InProgress;
            await Clients.Group(auctionId).SendAsync("AuctionResumed");
        }

        /// <summary>
        /// AUCTIONEER: Close auction permanently
        /// </summary>
        public async Task CloseAuctionAsync(string auctionId)
        {
            var email = GetUserEmail();
            if (string.IsNullOrEmpty(email)) return;

            var wrapper = GetCurrentWrapper(auctionId);
            if (wrapper == null || !IsAuctioneer(wrapper, email))
            {
                await Clients.Caller.SendAsync("Error", "Only auctioneers can close the auction");
                return;
            }

            wrapper.Wrapper.Status = AuctionStatus.Finished;
            await _auctionRepository.UpdateAsync(auctionId, wrapper.Wrapper);

            // Remove from memory
            _auctions.Remove(auctionId);

            await Clients.Group(auctionId).SendAsync("AuctionClosed");
        }

        /// <summary>
        /// Get all teams with their current rosters
        /// </summary>
        public async Task GetAllTeamsAsync(string auctionId)
        {
            var email = GetUserEmail();
            if (string.IsNullOrEmpty(email)) return;

            var wrapper = GetCurrentWrapper(auctionId);
            if (wrapper == null || !CanAccessAuction(wrapper, email)) return;


            await Clients.Caller.SendAsync("TeamsData", wrapper.Teams);
        }

        /// <summary>
        /// Get current player being auctioned
        /// </summary>
        public async Task GetCurrentPlayerAsync(string auctionId)
        {
            var email = GetUserEmail();
            if (string.IsNullOrEmpty(email)) return;

            var wrapper = GetCurrentWrapper(auctionId);
            if (wrapper == null || !CanAccessAuction(wrapper, email)) return;

            if (wrapper.Wrapper.Actual != null)
            {
                var player = wrapper.RealPlayers?.FirstOrDefault(x => x.Key == wrapper.Wrapper.Actual.Player.Key);
                if (player != null)
                {
                    await Clients.Caller.SendAsync("CurrentPlayer", player);
                }
            }
            else
            {
                await Clients.Caller.SendAsync("CurrentPlayer", null);
            }
        }

        public override async Task OnConnectedAsync()
        {
            var email = GetUserEmail();
            if (!string.IsNullOrEmpty(email))
            {
                await Clients.Caller.SendAsync("Connected", $"Welcome {email}");
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // User will be automatically removed from all groups
            await base.OnDisconnectedAsync(exception);
        }
    }
}

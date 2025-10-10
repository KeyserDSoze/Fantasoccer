using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Concurrent;
using Fantasoccer.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Org.BouncyCastle.Crypto;
using RepositoryFramework;

namespace Fantasoccer.Application.Api.Hubs
{
    [Authorize(Policy = "authenticated")]
    public sealed class AuctionHub : Hub
    {
        private readonly ILock _lock;
        private readonly IRepository<AuctionWrapper, string> _auctionRepository;
        private readonly IRepository<AppIdentity, string> _identityRepository;
        private readonly IRepository<Rank, LeagueKey> _rankRepository;
        private readonly IRepository<Team, TeamKey> _team;
        private readonly IRepository<Group, string> _groups;
        private readonly IRepository<StatPlayersWrapper, int> _statsPlayers;
        private static readonly Dictionary<string, AuctionHubWrapper> s_auctions = [];
        public AuctionHub(ILock @lock, IRepository<AuctionWrapper, string> auctionRepository,
            IRepository<AppIdentity, string> identityRepository,
            IRepository<Rank, LeagueKey> rankRepository,
           IRepository<Team, TeamKey> team, IRepository<Group, string> groups, IRepository<StatPlayersWrapper, int> statsPlayers)
        {
            _lock = @lock;
            _auctionRepository = auctionRepository;
            _identityRepository = identityRepository;
            _rankRepository = rankRepository;
            _team = team;
            _groups = groups;
            _statsPlayers = statsPlayers;
        }
        private async Task<AuctionHubWrapper?> GetCurrentWrapperAsync(string auctionId)
        {
            var added = false;
            if (!s_auctions.ContainsKey(auctionId))
            {
                await _lock.ExecuteAsync(async () =>
                 {
                     if (!s_auctions.ContainsKey(auctionId))
                     {
                         var auction = await _auctionRepository.GetAsync(auctionId);
                         if (auction == null)
                             throw new InvalidOperationException("Auction not found");
                         var group = await _groups.GetAsync(auction.LeagueKey.Group);
                         if (group != null)
                         {
                             var league = group.GetAnnualLeague(auction.LeagueKey.League, auction.LeagueKey.Year);
                             var leagueWrapper = group.Leagues.FirstOrDefault(x => x.Id == auction.LeagueKey.League);
                             if (league != null && leagueWrapper != null)
                             {
                                 var rank = await _rankRepository.GetAsync(auction.LeagueKey);
                                 var statPlayers = await _statsPlayers.GetAsync(auction.LeagueKey.Year);
                                 var teams = new List<Entity<Team, TeamKey>>();
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
                                         var teamInRank = rank?.RankedTeams.SelectMany(x => x.Value).FirstOrDefault(x => x.Owner == teamInYear.Owner);
                                         if (teamInRank != null && (league.Settings.MoneyForGoal > 0 || league.Settings.MoneyForSufferedGoal > 0))
                                         {
                                             team.MoneyFromRank = teamInRank.Goal * league.Settings.MoneyForGoal + teamInRank.SufferedGoal * league.Settings.MoneyForSufferedGoal;
                                         }
                                         teams.Add(new(team, teamKey));
                                     }
                                 }
                                 added = s_auctions.TryAdd(auctionId, new AuctionHubWrapper
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
                 });
            }
            s_auctions.TryGetValue(auctionId, out var wrapper);
            if (added && wrapper != null)
            {
                _ = UpdateAsync(auctionId);
            }
            return wrapper;
        }
        private async ValueTask UpdateAsync(string auctionId)
        {
            await Task.Delay(TimeSpan.FromSeconds(10));
            if (s_auctions.TryGetValue(auctionId, out var wrapper))
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
        private async Task<(bool Correct, string Message)> SetPlayerAsync(string auctionId)
        {
            var auctionWrapper = await GetCurrentWrapperAsync(auctionId);
            if (auctionWrapper?.RealPlayers == null)
                return (false, "Auction data not available");

            var current = auctionWrapper.Wrapper.Actual;

            if (current == null)
                return (false, "No player is currently being auctioned");

            var thePlayer = auctionWrapper.RealPlayers.FirstOrDefault(x => x.Key == current.Player.Key);
            if (thePlayer == null)
                return (false, "Player not found");

            var team = auctionWrapper.Teams.FirstOrDefault(x => x.Key.Email == current.Owner);
            if (team.Value == null)
                return (false, "Team not found");

            var substitutedPlayer = auctionWrapper.Wrapper.Kind == AuctionKind.Repairing ? auctionWrapper.RealPlayers.FirstOrDefault(x => x.Key == current.Substituted) : null;

            var check = Check(auctionWrapper.League.Settings, thePlayer, team.Value, current.Price, substitutedPlayer);
            if (string.IsNullOrWhiteSpace(check))
            {
                team.Value.Players.Add(new Player
                {
                    IsActive = true,
                    Name = thePlayer.Name,
                    RealTeam = thePlayer.RealTeam,
                    Price = current.Price,
                    Role = thePlayer.Role,
                    Status = PlayerInTeamStatus.Active,
                    Position = FantaSoccerRole.Tribune
                });
                if (substitutedPlayer != null)
                {
                    var toRemove = team.Value.Players.FirstOrDefault(x => x.Key == substitutedPlayer.Key && x.IsActive);
                    if (toRemove != null)
                    {
                        toRemove.Status = PlayerInTeamStatus.SoldWithNoReturnedPrice;
                    }
                }
                return (await _team.UpdateAsync(team.Key, team.Value), string.Empty);
            }
            else
            {
                return (false, check);
            }
        }
        private string Check(LeagueSetting settings, RealPlayer realPlayer, Team team, int price, RealPlayer? substitution)
        {
            switch (realPlayer.Role)
            {
                case Role.GoalKeeper:
                    if (team.ActivePlayers.Count(x => x.Role == realPlayer.Role) >= 3 + (substitution?.Role == Role.GoalKeeper ? 1 : 0))
                    {
                        return "Troppi portieri";
                    }
                    break;
                case Role.Defensor:
                    if (team.ActivePlayers.Count(x => x.Role == realPlayer.Role) >= 8 + (substitution?.Role == Role.Defensor ? 1 : 0))
                    {
                        return "Troppi difensori";
                    }
                    break;
                case Role.Midfielder:
                    if (team.ActivePlayers.Count(x => x.Role == realPlayer.Role) >= 8 + (substitution?.Role == Role.Midfielder ? 1 : 0))
                    {
                        return "Troppi centrocampisti";
                    }
                    break;
                case Role.Forward:
                    if (team.ActivePlayers.Count(x => x.Role == realPlayer.Role) >= 6 + (substitution?.Role == Role.Forward ? 1 : 0))
                    {
                        return "Troppi attaccanti";
                    }
                    break;
            }
            if (settings.StartingMoney - (team.Cost + price + (25 - 1 - team.ActivePlayers.Count())) < 0)
            {
                return "Stai spendendo troppi soldi";
            }
            return string.Empty;
        }
        private async Task<RealPlayer?> GetNextPlayerAsync(string auctionId, Role role)
        {
            var auctionWrapper = await GetCurrentWrapperAsync(auctionId);
            if (auctionWrapper?.Wrapper?.LastShow == null || auctionWrapper.RealPlayers == null)
                return null;

            var lastNameFound = auctionWrapper.Wrapper.LastShow[role];
            var foundPrevious = lastNameFound == null;
            foreach (var player in auctionWrapper.Wrapper.ListOfPlayers[role])
            {
                if (foundPrevious)
                {
                    var realPlayer = auctionWrapper.RealPlayers.First(x => x.Key == player);
                    auctionWrapper.Wrapper.LastShow[role] = player;
                    return realPlayer;
                }
                if (player == lastNameFound)
                {
                    foundPrevious = true;
                }
            }
            return null;
        }

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
                var wrapper = await GetCurrentWrapperAsync(auctionId);
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

            var wrapper = await GetCurrentWrapperAsync(auctionId);
            if (wrapper == null || !CanAccessAuction(wrapper, email)) return;

            await Clients.Caller.SendAsync("AuctionState", wrapper);
        }
        /// <summary>
        /// Place a bid on current player (both participants and auctioneer)
        /// </summary>
        public async Task PlaceBidAsync(string auctionId, int amount, string? bidderEmail = null, string? substituted = null)
        {
            var email = GetUserEmail();
            if (string.IsNullOrEmpty(email)) return;

            var wrapper = await GetCurrentWrapperAsync(auctionId);
            if (wrapper == null)
            {
                await Clients.Caller.SendAsync("Error", "Auction not found");
                return;
            }

            // Check if auction is active
            if (wrapper.Wrapper.Status == AuctionStatus.Finished)
            {
                await Clients.Caller.SendAsync("Error", "Auction is already finished");
                return;
            }

            if (wrapper.Wrapper.Status == AuctionStatus.Paused && !IsAuctioneer(wrapper, email))
            {
                await Clients.Caller.SendAsync("Error", "Auction is paused");
                return;
            }

            // Check if there's a player being auctioned
            if (wrapper.Wrapper.Actual == null)
            {
                await Clients.Caller.SendAsync("Error", "No player is currently being auctioned");
                return;
            }

            if (wrapper.Wrapper.Actual.StartingTime != null && DateTime.UtcNow.Subtract(wrapper.Wrapper.Actual.StartingTime.Value) > TimeSpan.FromSeconds(wrapper.Wrapper.SecondsPerAuction + 2)) //+2 is a window for slow client
            {
                await Clients.Caller.SendAsync("Error", "Bidding time for this player has expired");
                return;
            }

            // Determine who is bidding
            string actualBidder;
            if (IsAuctioneer(wrapper, email) && !string.IsNullOrEmpty(bidderEmail))
            {
                // Auctioneer bidding on behalf of someone
                actualBidder = bidderEmail;
            }
            else
            {
                // Regular participant bidding for themselves
                actualBidder = email;
            }

            if (wrapper.Wrapper.Actual.Owner == actualBidder)
            {
                await Clients.Caller.SendAsync("Error", "Sei già il miglior offerente.");
                return;
            }

            // Find bidder's team
            var teamEntity = wrapper.Teams.FirstOrDefault(x => x.Key.Email == actualBidder);
            if (teamEntity.Key == null || teamEntity.Value == null)
            {
                await Clients.Caller.SendAsync("Error", "Team not found");
                return;
            }
            var team = teamEntity.Value;

            await _lock.ExecuteAsync(async () =>
            {
                // Validate bid amount
                if (amount <= wrapper.Wrapper.Actual.Price)
                {
                    await Clients.Caller.SendAsync("Error", $"Bid must be higher than current bid (€{wrapper.Wrapper.Actual.Price})");
                    return;
                }

                // Check if same bidder is already highest
                if (!IsAuctioneer(wrapper, email) && wrapper.Wrapper.Actual.Owner == actualBidder)
                {
                    await Clients.Caller.SendAsync("Error", "You are already the highest bidder");
                    return;
                }
                var substitutedPlayer = wrapper.Wrapper.Kind == AuctionKind.Repairing ? wrapper.RealPlayers.FirstOrDefault(x => x.Key == substituted) : null;
                // Validate against team constraints
                var validationError = Check(wrapper.League.Settings, wrapper.Wrapper.Actual.Player, team, amount, substitutedPlayer);
                if (!string.IsNullOrWhiteSpace(validationError))
                {
                    await Clients.Caller.SendAsync("Error", validationError);
                    return;
                }

                if (substitutedPlayer != null && wrapper.Wrapper.Kind != AuctionKind.Repairing)
                {
                    await Clients.Caller.SendAsync("Error", "You can only substitute a player in repairing auctions");
                    return;
                }

                if (substitutedPlayer != null && substitutedPlayer.Role != wrapper.Wrapper.Actual.Player.Role)
                {
                    await Clients.Caller.SendAsync("Error", "You can only substitute a player with another of the same role");
                    return;
                }

                // Update bid
                wrapper.Wrapper.Actual.Price = amount;
                wrapper.Wrapper.Actual.Owner = actualBidder;
                wrapper.Wrapper.Actual.Substituted = substituted;
                wrapper.LastUpdate = DateTime.UtcNow;
                wrapper.Wrapper.Actual.StartingTime = DateTime.UtcNow;
                // Broadcast to all participants
                await Clients.Group(auctionId).SendAsync("BidPlaced", new
                {
                    Amount = amount,
                    BidderEmail = actualBidder,
                    BidderName = team.Name,
                    PlayerName = wrapper.Wrapper.Actual.Player.Name,
                    Substituted = wrapper.Wrapper.Actual.Substituted
                });

                // Send updated auction state to all
                await Clients.Group(auctionId).SendAsync("AuctionState", wrapper);
            });
        }

        /// <summary>
        /// AUCTIONEER: Show next player for bidding
        /// </summary>
        public async Task ShowNextPlayerAsync(string auctionId, Role role)
        {
            var email = GetUserEmail();
            if (string.IsNullOrEmpty(email)) return;

            var wrapper = await GetCurrentWrapperAsync(auctionId);
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
                Player = wrapper.RealPlayers?.First(x => x.Key == nextPlayer.Key) ?? throw new InvalidOperationException("Player not found"),
                Owner = null,
                Price = 0,
                Started = DateTime.UtcNow
            };
            wrapper.Wrapper.LastShow[role] = nextPlayer.Key;
            wrapper.Wrapper.Status = AuctionStatus.InProgress;

            // Broadcast to all participants
            await Clients.Group(auctionId).SendAsync("PlayerShown", wrapper.Wrapper.Actual);
        }

        public async Task ChangePlayerStatusAsync(string auctionId, string owner, string playerKey, PlayerInTeamStatus status, int? revenue)
        {
            var email = GetUserEmail();
            if (string.IsNullOrEmpty(email)) return;

            var wrapper = await GetCurrentWrapperAsync(auctionId);
            if (wrapper == null || !IsAuctioneer(wrapper, email))
            {
                await Clients.Caller.SendAsync("Error", "Only auctioneers can show next player");
                return;
            }

            if (status == PlayerInTeamStatus.Sold && revenue == null)
            {
                await Clients.Caller.SendAsync("Error", "Player sold with no price");
                return;
            }

            var teamEntity = wrapper.Teams.FirstOrDefault(x => x.Key.Email == owner);
            if (teamEntity?.Key == null || teamEntity.Value == null)
            {
                await Clients.Caller.SendAsync("Error", "Team not found");
                return;
            }

            var team = teamEntity.Value;
            var player = team.Players.FirstOrDefault(x => x.Key == playerKey);

            if (player == null)
            {
                await Clients.Caller.SendAsync("Error", "Player not found in team");
                return;
            }

            player.Status = status;
            if (status == PlayerInTeamStatus.Removed)
            {
                team.Players.Remove(player);
            }
            else if (status == PlayerInTeamStatus.Sold && revenue != null)
            {
                player.Revenue = revenue.Value;
            }
            else
            {
                player.Revenue = 0;
            }
            var updated = await _team.UpdateAsync(teamEntity.Key, team);
            if (updated)
            {
                await Clients.Group(auctionId).SendAsync("AuctionUpdated");
            }
            else
            {
                await Clients.Caller.SendAsync("Error", "Failed to update team");
            }

        }

        /// <summary>
        /// AUCTIONEER: Assign current player to a team
        /// </summary>
        public async Task AssignPlayerToTeamAsync(string auctionId)
        {
            var email = GetUserEmail();
            if (string.IsNullOrEmpty(email)) return;

            var wrapper = await GetCurrentWrapperAsync(auctionId);
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

            var (success, message) = await SetPlayerAsync(auctionId);

            if (!success)
            {
                await Clients.Caller.SendAsync("Error", message);
                return;
            }

            wrapper.Wrapper.Winnings.Add(new AuctionWin
            {
                PlayerKey = wrapper.Wrapper.Actual.Player.Key,
                Owner = wrapper.Wrapper.Actual.Owner,
                Price = wrapper.Wrapper.Actual.Price
            });
            var actual = wrapper.Wrapper.Actual;
            wrapper.Wrapper.Actual = null;

            // Get updated team
            var teamEntity = wrapper.Teams.FirstOrDefault(x => x.Key.Email == actual?.Owner);
            var team = teamEntity.Key != null ? teamEntity.Value : null;

            // Broadcast to all participants
            await Clients.Group(auctionId).SendAsync("PlayerAssigned", new
            {
                Owner = actual?.Owner,
                TeamName = team?.Name,
                Price = actual?.Price ?? 0,
                PlayerName = wrapper.RealPlayers?.FirstOrDefault(x => x.Key == actual?.Player.Key)?.Name ?? "Unknown",
                TeamCost = team?.Cost,
                TeamPlayersCount = team?.ActivePlayers.Count()
            });

            // Send updated state
            await Clients.Group(auctionId).SendAsync("AuctionUpdated");
        }

        public async Task ChangeTimerAsync(string auctionId, int timer)
        {
            var email = GetUserEmail();
            if (string.IsNullOrEmpty(email)) return;

            var wrapper = await GetCurrentWrapperAsync(auctionId);
            if (wrapper == null || !IsAuctioneer(wrapper, email))
            {
                await Clients.Caller.SendAsync("Error", "Only auctioneers can edit timer");
                return;
            }

            wrapper.Wrapper.SecondsPerAuction = timer;
            _ = UpdateAsync(auctionId);

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

            var wrapper = await GetCurrentWrapperAsync(auctionId);
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

            var wrapper = await GetCurrentWrapperAsync(auctionId);
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

            var wrapper = await GetCurrentWrapperAsync(auctionId);
            if (wrapper == null || !IsAuctioneer(wrapper, email))
            {
                await Clients.Caller.SendAsync("Error", "Only auctioneers can close the auction");
                return;
            }

            wrapper.Wrapper.Status = AuctionStatus.Finished;
            await _auctionRepository.UpdateAsync(auctionId, wrapper.Wrapper);

            // Remove from memory
            s_auctions.Remove(auctionId);

            await Clients.Group(auctionId).SendAsync("AuctionClosed");
        }

        /// <summary>
        /// Get current player being auctioned
        /// </summary>
        public async Task GetCurrentPlayerAsync(string auctionId)
        {
            var email = GetUserEmail();
            if (string.IsNullOrEmpty(email)) return;

            var wrapper = await GetCurrentWrapperAsync(auctionId);
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

        public async Task CompleteResetAsync(string auctionId)
        {
            var email = GetUserEmail();
            var wrapper = await GetCurrentWrapperAsync(auctionId);
            if (wrapper == null || !IsAuctioneer(wrapper, email!))
            {
                await Clients.Caller.SendAsync("Error", "Only auctioneers can reset the auction");
                return;
            }
            if (wrapper.Wrapper.Actual == null)
            {
                await Clients.Caller.SendAsync("Error", "No player is currently being auctioned");
                return;
            }
            wrapper.Wrapper.Actual.Owner = null;
            wrapper.Wrapper.Actual.Price = 0;
            wrapper.Wrapper.Actual.Substituted = null;
            wrapper.Wrapper.Actual.StartingTime = null;
            await Clients.Group(auctionId).SendAsync("CurrentAuctionReset");
        }
        public async Task TimerResetAsync(string auctionId)
        {
            var email = GetUserEmail();
            var wrapper = await GetCurrentWrapperAsync(auctionId);
            if (wrapper == null || !IsAuctioneer(wrapper, email!))
            {
                await Clients.Caller.SendAsync("Error", "Only auctioneers can reset the auction");
                return;
            }
            if (wrapper.Wrapper.Actual == null)
            {
                await Clients.Caller.SendAsync("Error", "No player is currently being auctioned");
                return;
            }
            wrapper.Wrapper.Actual.StartingTime = DateTime.UtcNow;
            await Clients.Group(auctionId).SendAsync("TimerReset");
        }
    }
}

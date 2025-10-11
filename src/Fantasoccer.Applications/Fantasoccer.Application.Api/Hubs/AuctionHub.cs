using System.Text;
using System.Threading.Concurrent;
using Fantasoccer.Business;
using Fantasoccer.Business.Services;
using Fantasoccer.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Org.BouncyCastle.Crypto;
using RepositoryFramework;

namespace Fantasoccer.Application.Api.Hubs
{
    [Authorize(Policy = "authenticated")]
    public sealed class AuctionHub : Hub
    {
        private readonly AuctionHubService _auctionHubService;
        private readonly IRepository<AppIdentity, string> _identityRepository;
        private readonly EncryptionService _encryptionService;
        private readonly IRepository<Team, TeamKey> _team;
        public AuctionHub(AuctionHubService auctionHubService,
            IRepository<AppIdentity, string> identityRepository,
            EncryptionService encryptionService,
            IRepository<Team, TeamKey> team)
        {
            _auctionHubService = auctionHubService;
            _identityRepository = identityRepository;
            _encryptionService = encryptionService;
            _team = team;
        }
        private sealed class ConcurrencyResponse
        {
            public bool IsOk { get; }
            public string? Message { get; }
            public object? Data { get; }
            public ConcurrencyResponse(bool isOk, AnyOf<string, object>? data = null)
            {
                IsOk = isOk;
                if (data?.IsT0 == true)
                {
                    Message = data.CastT0;
                }
                else if (data?.IsT1 == true)
                {
                    Data = data.CastT1;
                }
            }
        }
        private async Task<(bool Correct, string Message)> SetPlayerAsync(AuctionHubWrapper auctionWrapper)
        {
            if (auctionWrapper?.RealPlayers == null)
                return (false, "Auction data not available");

            var current = auctionWrapper.Wrapper.Actual;

            if (current == null)
                return (false, "No player is currently being auctioned");

            var thePlayer = auctionWrapper.RealPlayers.FirstOrDefault(x => x.Key == current.Player.Key);
            if (thePlayer == null)
                return (false, "Player not found");

            var team = auctionWrapper.Teams.FirstOrDefault(x => x.Key?.Email == current.Owner);
            if (team?.Value == null)
                return (false, "Team not found");

            var substitutedPlayer = auctionWrapper.Wrapper.Kind == AuctionKind.Repairing ? auctionWrapper.RealPlayers.FirstOrDefault(x => x.Key == current.Substituted) : null;

            var check = Check(auctionWrapper.League!.Settings, thePlayer, team.Value, current.Price, substitutedPlayer);
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
        private StatPlayer? GetNextPlayer(AuctionHubWrapper auctionWrapper, Role role)
        {
            if (auctionWrapper.RealPlayers == null)
                return null;

            foreach (var player in auctionWrapper.Wrapper.ListOfPlayers[role])
            {
                if (!player.IsShowed)
                {
                    player.IsShowed = true;
                    var playerKey = _encryptionService.Decrypt(player.EncryptedKey);
                    return auctionWrapper.RealPlayers.FirstOrDefault(x => x.Key == playerKey);
                }
            }
            return null;
        }
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
        private async ValueTask<bool> IsViewer(AuctionHubWrapper wrapper, string userEmail)
        {
            var identity = await _identityRepository.GetAsync(userEmail);
            return identity?.Groups.Any(t => t.Id == wrapper.Group?.Id) == true;
        }
        /// <summary>
        /// Check if user can access this auction (either auctioneer or participant)
        /// </summary>
        private async ValueTask<(bool CanAccess, bool IsViewer)> CanAccessAuctionAsync(AuctionHubWrapper wrapper, string userEmail)
        {
            if (IsAuctioneer(wrapper, userEmail) || IsParticipant(wrapper, userEmail))
                return (true, false);
            var isViewer = await IsViewer(wrapper, userEmail);
            return (isViewer, isViewer);
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

            var concurrencyResponse = await _auctionHubService.ExecuteOperationAsync(auctionId, async (wrapper) =>
            {
                try
                {
                    if (wrapper == null)
                        return new ConcurrencyResponse(false, "Auction not found");

                    var canAccessResult = await CanAccessAuctionAsync(wrapper, email);
                    if (!canAccessResult.CanAccess)
                        return new ConcurrencyResponse(false, "You don't have access to this auction");

                    // Add to participants if not already there
                    if (!canAccessResult.IsViewer && !wrapper.Wrapper.Participants.Any(x => x.Owner == email))
                    {
                        var team = wrapper.Teams.FirstOrDefault(x => x.Key.Email == email);
                        if (team?.Value != null)
                        {
                            wrapper.Wrapper.Participants.Add(new AnnualTeam
                            {
                                Name = team.Value.Name,
                                Owner = email,
                                AdditionalOwners = []
                            });
                        }
                    }
                    return new ConcurrencyResponse(true);
                }
                catch (Exception ex)
                {
                    return new ConcurrencyResponse(false, $"Failed to join auction: {ex.Message}");
                }
            });
            // Notify others (fire-and-forget to avoid blocking)
            var check = concurrencyResponse?.IsOk == true;
            if (check)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, auctionId);
                _ = Clients.OthersInGroup(auctionId).SendAsync("UserJoined", email);
            }
            else
                await Clients.Caller.SendAsync("Error", concurrencyResponse?.Message ?? "Generic Error");
            return check;
        }

        /// <summary>
        /// Leave auction room
        /// </summary>
        public async Task LeaveAuctionAsync(string auctionId)
        {
            var email = GetUserEmail();
            if (string.IsNullOrEmpty(email)) return;

            var concurrencyResponse = await _auctionHubService.ExecuteOperationAsync(auctionId, async (wrapper) =>
            {
                try
                {
                    if (wrapper == null)
                    {
                        return new ConcurrencyResponse(false, "Auction not found");
                    }

                    var canAccessResult = await CanAccessAuctionAsync(wrapper, email);
                    if (!canAccessResult.CanAccess)
                    {
                        return new ConcurrencyResponse(false, "You don't have access to this auction");
                    }

                    // Add to participants if not already there
                    if (!canAccessResult.IsViewer)
                    {
                        var annualTeam = wrapper.Wrapper.Participants.FirstOrDefault(x => x.Owner == email);
                        if (annualTeam != null)
                        {
                            wrapper.Wrapper.Participants.Remove(annualTeam);
                        }
                    }

                    return new ConcurrencyResponse(true);
                }
                catch (Exception ex)
                {
                    return new ConcurrencyResponse(false, $"Failed to join auction: {ex.Message}");
                }
            });
            if (concurrencyResponse?.IsOk == true)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, auctionId);
                // Notify others (fire-and-forget to avoid blocking)
                _ = Clients.OthersInGroup(auctionId).SendAsync("UserLeft", email);
            }
            else
            {
                await Clients.Caller.SendAsync("Error", concurrencyResponse?.Message ?? "Generic Error");
            }
        }

        /// <summary>
        /// Place a bid on current player (both participants and auctioneer)
        /// </summary>
        public async Task PlaceBidAsync(string auctionId, int amount, string? bidderEmail = null, string? substituted = null)
        {
            var email = GetUserEmail();
            if (string.IsNullOrEmpty(email)) return;

            var concurrencyResponse = await _auctionHubService.ExecuteOperationAsync(auctionId, async (wrapper) =>
            {
                if (wrapper == null)
                {
                    return new ConcurrencyResponse(false, "Auction not found");
                }

                // Check if auction is active
                if (wrapper.Wrapper.Status == AuctionStatus.Finished)
                {
                    return new ConcurrencyResponse(false, "Auction is already finished");
                }

                if (wrapper.Wrapper.Status == AuctionStatus.Paused && !IsAuctioneer(wrapper, email))
                {
                    return new ConcurrencyResponse(false, "Auction is paused");
                }

                // Check if there's a player being auctioned
                if (wrapper.Wrapper.Actual == null)
                {
                    return new ConcurrencyResponse(false, "No player is currently being auctioned");
                }

                if (wrapper.Wrapper.Actual.StartingTime != null && DateTime.UtcNow.Subtract(wrapper.Wrapper.Actual.StartingTime.Value) > TimeSpan.FromSeconds(wrapper.Wrapper.SecondsPerAuction + 2)) //+2 is a window for slow client
                {
                    return new ConcurrencyResponse(false, "Bidding time for this player has expired");
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
                    return new ConcurrencyResponse(false, "Sei già il miglior offerente.");
                }

                // Find bidder's team
                var teamEntity = wrapper.Teams.FirstOrDefault(x => x.Key.Email == actualBidder);
                if (teamEntity?.Key == null || teamEntity.Value == null)
                {
                    return new ConcurrencyResponse(false, "Team not found");
                }
                var team = teamEntity.Value;


                // Validate bid amount
                if (amount <= wrapper.Wrapper.Actual.Price)
                {
                    return new ConcurrencyResponse(false, $"Bid must be higher than current bid (${wrapper.Wrapper.Actual.Price})");
                }

                // Check if same bidder is already highest
                if (!IsAuctioneer(wrapper, email) && wrapper.Wrapper.Actual.Owner == actualBidder)
                {
                    return new ConcurrencyResponse(false, "You are already the highest bidder");
                }
                var substitutedPlayer = wrapper.Wrapper.Kind == AuctionKind.Repairing ? wrapper.RealPlayers.FirstOrDefault(x => x.Key == substituted) : null;
                // Validate against team constraints
                var validationError = Check(wrapper.League.Settings, wrapper.Wrapper.Actual.Player, team, amount, substitutedPlayer);
                if (!string.IsNullOrWhiteSpace(validationError))
                {
                    return new ConcurrencyResponse(false, validationError);
                }

                if (substitutedPlayer != null && wrapper.Wrapper.Kind != AuctionKind.Repairing)
                {
                    return new ConcurrencyResponse(false, "You can only substitute a player in repairing auctions");
                }

                if (substitutedPlayer != null && substitutedPlayer.Role != wrapper.Wrapper.Actual.Player.Role)
                {
                    return new ConcurrencyResponse(false, "You can only substitute a player with another of the same role");
                }

                // Update bid
                wrapper.Wrapper.Actual.Price = amount;
                wrapper.Wrapper.Actual.Owner = actualBidder;
                wrapper.Wrapper.Actual.Substituted = substituted;
                wrapper.LastUpdate = DateTime.UtcNow;
                wrapper.Wrapper.Actual.StartingTime = DateTime.UtcNow;
                return new ConcurrencyResponse(true, new AuctionBid()
                {
                    Amount = amount,
                    BidderEmail = actualBidder,
                    BidderName = team.Name,
                    PlayerName = wrapper.Wrapper.Actual.Player.Name,
                    Substituted = wrapper.Wrapper.Actual.Substituted
                });
            });
            if (concurrencyResponse?.IsOk == true)
            {
                // Broadcast to all participants
                await Clients.Group(auctionId).SendAsync("BidPlaced", concurrencyResponse.Data);
            }
            else
            {
                await Clients.Caller.SendAsync("Error", concurrencyResponse?.Message ?? "Generic Error");
            }
        }

        /// <summary>
        /// AUCTIONEER: Show next player for bidding
        /// </summary>
        public async Task ShowNextPlayerAsync(string auctionId, Role role, string? forcedPlayerKey = null)
        {
            var email = GetUserEmail();
            if (string.IsNullOrEmpty(email)) return;

            var concurrencyResponse = await _auctionHubService.ExecuteOperationAsync(auctionId, (wrapper) =>
            {
                if (wrapper == null || !IsAuctioneer(wrapper, email))
                {
                    return Task.FromResult(new ConcurrencyResponse(false, "Only auctioneers can show next player"));
                }

                if (wrapper.Wrapper.Status == AuctionStatus.Finished)
                {
                    return Task.FromResult(new ConcurrencyResponse(false, "Auction is already finished"));
                }

                if (wrapper.Wrapper.Actual?.Owner != null)
                {
                    return Task.FromResult(new ConcurrencyResponse(false, "Auction for player not terminated"));
                }

                var nextPlayer = forcedPlayerKey != null ? wrapper.RealPlayers!.FirstOrDefault(x => x.Key == forcedPlayerKey) : GetNextPlayer(wrapper, role);
                if (nextPlayer == null)
                {
                    return Task.FromResult(new ConcurrencyResponse(false, $"No more players for role {role}"));
                }

                // Update auction state
                wrapper.Wrapper.Actual = new ActualAuction
                {
                    Player = nextPlayer ?? throw new InvalidOperationException("Player not found"),
                    Owner = null,
                    Price = 0,
                    Started = DateTime.UtcNow
                };
                wrapper.Wrapper.LastShow[role] = nextPlayer.Key;
                wrapper.Wrapper.Status = AuctionStatus.InProgress;

                return Task.FromResult(new ConcurrencyResponse(true, wrapper.Wrapper.Actual));
            });
            // Broadcast to all participants
            if (concurrencyResponse?.IsOk == true)
            {
                await Clients.Group(auctionId).SendAsync("PlayerShown", concurrencyResponse?.Data);
            }
            else
            {
                await Clients.Caller.SendAsync("Error", concurrencyResponse?.Message ?? "Generic Error");
            }
        }

        public async Task ChangePlayerStatusAsync(string auctionId, string owner, string playerKey, PlayerInTeamStatus status, int? revenue)
        {
            var email = GetUserEmail();
            if (string.IsNullOrEmpty(email)) return;
            var concurrentResponse = await _auctionHubService.ExecuteOperationAsync(auctionId, async (wrapper) =>
            {
                if (wrapper == null || !IsAuctioneer(wrapper, email))
                {
                    return new ConcurrencyResponse(false, "Only auctioneers can change player status");
                }

                if (status == PlayerInTeamStatus.Sold && revenue == null)
                {
                    return new ConcurrencyResponse(false, "Player sold with no price");
                }

                var teamEntity = wrapper.Teams.FirstOrDefault(x => x.Key.Email == owner);
                if (teamEntity?.Key == null || teamEntity.Value == null)
                {
                    return new ConcurrencyResponse(false, "Team not found");
                }

                var team = teamEntity.Value;
                var player = team.Players.FirstOrDefault(x => x.Key == playerKey);

                if (player == null)
                {
                    return new ConcurrencyResponse(false, "Player not found in team");
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
                return new ConcurrencyResponse(true, teamEntity);
            });
            if (concurrentResponse?.IsOk == true)
            {
                await Clients.Group(auctionId).SendAsync("TeamUpdated", concurrentResponse.Data);
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

            var concurrencyResponse = await _auctionHubService.ExecuteOperationAsync(auctionId, async (wrapper) =>
            {
                if (wrapper == null || !IsAuctioneer(wrapper, email))
                {
                    return new ConcurrencyResponse(false, "Only auctioneers can assign players");
                }

                if (wrapper.Wrapper.Actual == null)
                {
                    return new ConcurrencyResponse(false, "No player is currently being auctioned");
                }

                var (success, message) = await SetPlayerAsync(wrapper);

                if (!success)
                {
                    return new ConcurrencyResponse(false, message);
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
                var team = teamEntity?.Key != null ? teamEntity.Value : null;
                return new ConcurrencyResponse(true, new PlayerAssignedMessageInAuction
                {
                    PlayerAssignedInAuction = new()
                    {
                        Owner = actual?.Owner,
                        TeamName = team?.Name,
                        Price = actual?.Price ?? 0,
                        PlayerName = wrapper.RealPlayers?.FirstOrDefault(x => x.Key == actual?.Player.Key)?.Name ?? "Unknown",
                        TeamCost = team?.Cost ?? 0,
                        TeamPlayersCount = team?.ActivePlayers.Count() ?? 0
                    },
                    Team = teamEntity
                });
            });
            if (concurrencyResponse?.IsOk == true)
            {
                // Broadcast to all participants
                await Clients.Group(auctionId).SendAsync("PlayerAssigned", concurrencyResponse.Data);
            }
            else
            {
                await Clients.Caller.SendAsync("Error", concurrencyResponse?.Message ?? "Generic Error");
            }
        }

        public async Task ChangeTimerAsync(string auctionId, int timer)
        {
            var email = GetUserEmail();
            if (string.IsNullOrEmpty(email)) return;

            var concurrencyResponse = await _auctionHubService.ExecuteOperationAsync(auctionId, (wrapper) =>
            {
                if (wrapper == null || !IsAuctioneer(wrapper, email))
                {
                    return Task.FromResult(new ConcurrencyResponse(false, "Only auctioneer can edit timer"));
                }
                wrapper.Wrapper.SecondsPerAuction = timer;
                return Task.FromResult(new ConcurrencyResponse(true));
            });
            // Send updated state
            if (concurrencyResponse?.IsOk == true)
            {
                await Clients.Group(auctionId).SendAsync("TimerUpdate", timer);
            }
            else
            {
                await Clients.Caller.SendAsync("Error", concurrencyResponse?.Message ?? "Generic Error");
            }
        }

        /// <summary>
        /// AUCTIONEER: Pause auction
        /// </summary>
        public Task PauseAuctionAsync(string auctionId)
        {
            return ChangeAuctionStateAsync(auctionId, AuctionStatus.Paused, "pause");
        }

        /// <summary>
        /// AUCTIONEER: Resume auction
        /// </summary>
        public Task ResumeAuctionAsync(string auctionId)
        {
            return ChangeAuctionStateAsync(auctionId, AuctionStatus.InProgress, "resume");
        }

        /// <summary>
        /// AUCTIONEER: Close auction permanently
        /// </summary>
        public Task CloseAuctionAsync(string auctionId)
        {
            return ChangeAuctionStateAsync(auctionId, AuctionStatus.Finished, "close");
        }

        private async Task ChangeAuctionStateAsync(string auctionId, AuctionStatus status, string eventName)
        {
            var email = GetUserEmail();
            if (string.IsNullOrEmpty(email)) return;

            var concurrencyResponse = await _auctionHubService.ExecuteOperationAsync(auctionId, (wrapper) =>
            {
                if (wrapper == null || !IsAuctioneer(wrapper, email))
                {
                    return Task.FromResult(new ConcurrencyResponse(false, $"Only auctioneer can {eventName} the auction"));
                }
                wrapper.Wrapper.Status = status;

                return Task.FromResult(new ConcurrencyResponse(true));

            });
            // Send updated state
            if (concurrencyResponse?.IsOk == true)
            {
                _ = Clients.Group(auctionId).SendAsync($"Auction{eventName.ToUpperCaseFirst()}d");
            }
            else
            {
                await Clients.Caller.SendAsync("Error", concurrencyResponse?.Message ?? "Generic Error");
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
            var concurrencyResponse = await _auctionHubService.ExecuteOperationAsync(auctionId, (wrapper) =>
            {
                if (wrapper == null || !IsAuctioneer(wrapper, email!))
                {
                    return Task.FromResult(new ConcurrencyResponse(false, "Only auctioneers can reset the auction"));
                }
                if (wrapper.Wrapper.Actual == null)
                {
                    return Task.FromResult(new ConcurrencyResponse(false, "No player is currently being auctioned"));
                }
                wrapper.Wrapper.Actual.Owner = null;
                wrapper.Wrapper.Actual.Price = 0;
                wrapper.Wrapper.Actual.Substituted = null;
                wrapper.Wrapper.Actual.StartingTime = null;
                return Task.FromResult(new ConcurrencyResponse(true));
            });
            if (concurrencyResponse?.IsOk == true)
            {
                _ = Clients.Group(auctionId).SendAsync("CurrentAuctionReset");
            }
            else
            {
                await Clients.Caller.SendAsync("Error", concurrencyResponse?.Message ?? "Generic Error");
            }
        }
        public async Task TimerResetAsync(string auctionId)
        {
            var email = GetUserEmail();
            var concurrencyResponse = await _auctionHubService.ExecuteOperationAsync(auctionId, (wrapper) =>
            {
                if (wrapper == null || !IsAuctioneer(wrapper, email!))
                {
                    return Task.FromResult(new ConcurrencyResponse(false, "Only auctioneers can reset the auction"));
                }
                if (wrapper.Wrapper.Actual == null)
                {
                    return Task.FromResult(new ConcurrencyResponse(false, "No player is currently being auctioned"));
                }
                wrapper.Wrapper.Actual.StartingTime = DateTime.UtcNow;
                return Task.FromResult(new ConcurrencyResponse(true));
            });
            if (concurrencyResponse?.IsOk == true)
            {
                _ = Clients.Group(auctionId).SendAsync("TimerReset");
            }
            else
            {
                await Clients.Caller.SendAsync("Error", concurrencyResponse?.Message ?? "Generic Error");
            }
        }
    }
}

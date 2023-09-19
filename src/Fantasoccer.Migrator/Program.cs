// See https://aka.ms/new-console-template for more information
using System.Drawing;
using System.Text.Json;
using FantacalcioV4.ApiManager;
using Fantasoccer;
using Fantasoccer.Business;
using Fantasoccer.Infrastructure;
using Fantasoccer.Migrator.FalTeam;
using Mapster;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;
using Rystem;

namespace Migrator
{
    public static class EntryPoint
    {
        public static async Task Main(string[] args)
        {
            var services = new ServiceCollection();
            var storageAccountName = "superfc";
            var storageAccountKey = "CS124aZwZywrMrxFiCdGFipSN+UbExX1BPEb/9aY1dWGLdSboO2drxh3yFSFOVM6uGc7Grahs/sZcpDRtP77yg==";
            var newStorage = "DefaultEndpointsProtocol=https;AccountName=fasostorage;AccountKey=R52c5CERE/WGuEiE+8U7sBg8zoy2/zB0hmtPPmHKgvABfCvoQn/d3YHo4EG42Wx3iKkcY6EYxpEX+AStxkydZw==;EndpointSuffix=core.windows.net";
            services
                .AddRystem()
                .AddAzureService()
                .AddStorage(new Rystem.Azure.Integration.Storage.StorageAccount(storageAccountName, storageAccountKey));
            services.AddFantacalcioApi();
            //services.AddInMemoryInfrastructures();
            await services.AddStorageInfrastructuresAsync(newStorage);
            services.AddFantasoccerBusiness();
            services.AddSharedBusiness();
            var serviceProvider = services.BuildServiceProvider().CreateScope().ServiceProvider;
            serviceProvider.UseRystem();
            await serviceProvider.WarmUpAsync();
            var manager = serviceProvider.GetRequiredService<Manager>();
            //await CreateGroupAndMapUsersAsync(serviceProvider, manager);
            //await ParseSerieAVotesAndCalendar(serviceProvider, manager);
            //await ParseOldFormationsAndCalendarAsync(serviceProvider, manager, true);
            //await AdjustLeagueAsync(serviceProvider);
            //await CreateCalendarAndRankAsync(serviceProvider);
            //await CreateStatsPlayerAsync(serviceProvider);
            //await CreateNewYear(serviceProvider);
            //await AdjustFormation(serviceProvider);
            await AdjustCalendar(serviceProvider);
        }
        private static async Task AdjustCalendar(IServiceProvider serviceProvider)
        {
            var calendarR = serviceProvider.GetRequiredService<IRepository<Calendar, LeagueKey>>();
            var group = serviceProvider.GetRequiredService<IRepository<Group, string>>();
            var firstGroup = await group.FirstOrDefaultAsync();
            var leagueKey = new LeagueKey
            {
                Year = FantacalcioTime.ActualYear,
                Group = firstGroup.Key,
                League = firstGroup.Value.Leagues.First(x => !x.IsMain && x.Type == LeagueType.League).Id
            };
            var calendar = await calendarR.GetAsync(leagueKey);
            foreach (var round in calendar.Rounds)
            {
                foreach (var day in round.Value)
                {
                    if (day.SerieADay <= 4)
                    {
                        foreach (var game in day.Games)
                        {
                            game.Result = new GameResult { IsCancelled = true };
                        }
                    }
                }
            }
            await calendarR.UpdateAsync(leagueKey, calendar);
        }
        private static async Task AdjustFormation(IServiceProvider serviceProvider)
        {
            var teamRepository = serviceProvider.GetRequiredService<IRepository<Team, TeamDayKey>>();
            var groupRepository = serviceProvider.GetRequiredService<IRepository<Group, string>>();
            var group = await groupRepository.FirstOrDefaultAsync();
            var key = new TeamDayKey
            {
                Year = FantacalcioTime.ActualYear,
                Group = group.Key,
                BasketId = group.Value.Baskets.First().Id,
                Day = 5,
                Email = "baghiniluca@gmail.com"
            };
            var lTeam = await teamRepository.GetAsync(key);
            if (lTeam != null && lTeam.Players != null && lTeam.Players.Count > 0)
            {
                key.Day = 4;
                await teamRepository.UpdateAsync(key, lTeam);
            }
        }
        private static async Task CreateStatsPlayerAsync(IServiceProvider serviceProvider)
        {
            var stat = serviceProvider.GetRequiredService<IStatsPlayerGenerator>();
            for (var i = 1; i < 12; i++)
            {
                await stat.GenerateAndSaveAsync(i, 38);
                Console.WriteLine($"Created year {i}");
            }
        }
        private static async Task CreateGroupAndMapUsersAsync(IServiceProvider serviceProvider, Manager manager)
        {
            var groupRepository = serviceProvider.GetRequiredService<IRepository<Group, string>>();
            var userRepository = serviceProvider.GetRequiredService<IRepository<AppIdentity, string>>();
            var teamRepository = serviceProvider.GetRequiredService<IRepository<Team, TeamKey>>();
            var groups = await manager.Group.ListAsync();
            var leagues = new List<string> { "Serie A", "Premier League", "Champions League", "Super League" };
            foreach (var groupId in groups)
            {
                Console.WriteLine(groupId);
                var group = new Group()
                {
                    Name = "Fantacalcio",
                    Baskets = new(),
                    Leagues = new(),
                    Id = Guid.NewGuid().ToString(),
                    Users = new(),
                };
                for (var year = 1; year < 12; year++)
                {
                    foreach (var leagueId in leagues)
                    {
                        var league = await manager.League.GetAsync(groupId, leagueId, year);
                        if (league != null)
                        {
                            var actualLeague = group.Leagues.Find(x => x.Name == league.Name);
                            if (actualLeague == null)
                            {
                                actualLeague = new League
                                {
                                    BasketsId = new(),
                                    Id = Guid.NewGuid().ToString(),
                                    IsMain = league.Name == "Serie A" || league.Name == "Premier League",
                                    Name = league.Name,
                                    Type = (LeagueType)(int)league.Type,
                                    Years = new()
                                };
                                group.Leagues.Add(actualLeague);
                            }
                            var newSettings = league.Settings.Adapt<LeagueSetting>();
                            actualLeague.Years.Add(new AnnualLeague
                            {
                                Year = year,
                                Settings = newSettings,
                            });
                            //Basket creation
                            if (actualLeague.IsMain)
                            {
                                var actualBasket = group.Baskets.FirstOrDefault(x => x.Name == actualLeague.Name);
                                if (actualBasket == null)
                                {
                                    actualBasket = new Basket
                                    {
                                        Id = Guid.NewGuid().ToString(),
                                        Name = league.Name,
                                        Years = new()
                                    };
                                    group.Baskets.Add(actualBasket);
                                }
                                actualLeague.BasketsId.Clear();
                                actualLeague.BasketsId.Add(actualBasket.Id);
                                var rank = await manager.League.GetRankAsync(groupId, leagueId, year);
                                actualBasket.Years.Add(new YearlyBasket
                                {
                                    Year = year,
                                    Teams = new()
                                });
                                foreach (var team in rank.RankedTeams)
                                {
                                    actualBasket.Years.Last().Teams.Add(new AnnualTeam
                                    {
                                        Name = team.Name,
                                        Owner = CalculateOwner(team.Username),
                                    });
                                }
                            }
                            else
                            {
                                actualLeague.BasketsId = group.Baskets.Select(x => x.Id).ToList();
                            }
                        }
                        Console.WriteLine($"Inserted {groupId} - {leagueId} - {year}");
                    }
                }
                foreach (var leagueId in leagues)
                {
                    var actualLeague = group.Leagues.Find(x => x.Name == leagueId);
                    var newSettings = LeagueSetting.Default;
                    actualLeague.Years.Add(new AnnualLeague
                    {
                        Year = 12,
                        Settings = newSettings,
                    });
                    //Basket creation
                    if (actualLeague.IsMain)
                    {
                        var actualBasket = group.Baskets.FirstOrDefault(x => x.Name == actualLeague.Name);
                        actualLeague.BasketsId.Clear();
                        actualLeague.BasketsId.Add(actualBasket.Id);
                        actualBasket.Years.Add(new YearlyBasket
                        {
                            Year = 12,
                            Teams = new()
                        });
                        foreach (var mapperUser in s_mappedUsers)
                        {
                            var email = mapperUser.Value.Split("|||").First();
                            actualBasket.Years.Last().Teams.Add(new AnnualTeam
                            {
                                Name = $"{email} team",
                                Owner = CalculateOwner(email),
                            });
                        }
                    }
                    else
                    {
                        actualLeague.BasketsId = group.Baskets.Select(x => x.Id).ToList();
                    }
                    Console.WriteLine("Year 12");
                }
                if (!await groupRepository.ExistAsync(group.Id))
                    await groupRepository.InsertAsync(group.Id, group);
                else
                    await groupRepository.UpdateAsync(group.Id, group);
                Console.WriteLine($"Group finalized {group.Name}");
            }
        }
        private static async Task CreateNewYear(IServiceProvider serviceProvider)
        {
            var groupRepository = serviceProvider.GetRequiredService<IRepository<Group, string>>();
            var userRepository = serviceProvider.GetRequiredService<IRepository<AppIdentity, string>>();
            var teamRepository = serviceProvider.GetRequiredService<IRepository<Team, TeamKey>>();
            var group = (await groupRepository.FirstOrDefaultAsync()).Value;
            group.Users.Clear();
            var path = @"E:\fantasoccer";
            foreach (var basket in group.Baskets)
            {
                basket.Years.First(x => x.Year == 12).Teams.Clear();
            }
            foreach (var mapperUser in s_mappedUsers)
            {
                var email = mapperUser.Value.Split("|||").First();
                var leagueName = mapperUser.Value.Split("|||").Last();
                group.Users.Add(new UserOfAGroup
                {
                    Email = email,
                    Username = mapperUser.Key,
                    Role = email == "alessandro.rapiti44@gmail.com" ? IdentityRole.Participant | IdentityRole.Admin : IdentityRole.Participant
                });
                var league = group.Leagues.First(x => x.Name == leagueName);
                var basket = group.Baskets.First(x => x.Name == leagueName).Years.First(x => x.Year == 12).Teams;
                basket.Add(new AnnualTeam
                {
                    Name = $"{email} team",
                    Owner = email
                });
                var identity = new AppIdentity
                {
                    Email = email,
                    State = new()
                    {
                        GroupId = group.Id,
                        MeOrLeaderEmail = email,
                        Year = FantacalcioTime.ActualYear,
                        LeagueId = league.Id
                    },
                    Username = mapperUser.Key,
                    Groups = new List<GroupForIdentity> { group.ToGroupIdentity(email) }
                };
                await userRepository.UpdateAsync(email, identity);
                Console.WriteLine($"Added {email} from {mapperUser.Key} to {group.Name}");
                var file = new DirectoryInfo(path).GetFiles().FirstOrDefault(x => x.FullName.Split("%7C").Last() == email);
                if (file != null)
                {
                    using var reader = new StreamReader(file.OpenRead());
                    var teamAsJson = await reader.ReadToEndAsync();
                    var teamX = teamAsJson.FromJson<RootTeam>().Value;
                    var newTeam = new Team
                    {
                        AdditionalOwners = teamX.AdditionalOwners,
                        Owner = teamX.Owner,
                        Name = teamX.Name,
                        MoneyFromRank = teamX.MoneyFromRank,
                        RevenueMoney = teamX.RevenueMoney,
                        Players = teamX.Players.Select(x => new Player
                        {
                            IsActive = x.IsActive,
                            Name = x.Name,
                            Position = x.Position,
                            Price = x.Price,
                            Role = x.Role,
                            Status = x.Status,
                            RealTeam = new RealTeam() { Name = x.RealTeam.Name }
                        }).ToList()
                    };
                    await teamRepository.UpdateAsync(new TeamKey
                    {
                        Year = 12,
                        Email = teamX.Owner,
                        BasketId = group.GetBasketId(email, 12)!,
                        Group = group.Id
                    }, newTeam);
                    Console.WriteLine($"Added new team for {teamX.Owner}");
                }
                else
                {
                    var newTeam = new Team
                    {
                        AdditionalOwners = null,
                        Owner = email,
                        Name = $"{email} team",
                        MoneyFromRank = 0,
                        RevenueMoney = 0,
                        Players = new()
                    };
                    await teamRepository.UpdateAsync(new TeamKey
                    {
                        Year = 12,
                        Email = email,
                        BasketId = group.GetBasketId(email, 12)!,
                        Group = group.Id
                    }, newTeam);
                    Console.WriteLine($"Added new empty team for {email}");
                }
            }
            if (!await groupRepository.ExistAsync(group.Id))
                await groupRepository.InsertAsync(group.Id, group);
            else
                await groupRepository.UpdateAsync(group.Id, group);
        }
        private static async Task ParseSerieAVotesAndCalendar(IServiceProvider serviceProvider, Manager manager)
        {
            var calendarRepository = serviceProvider.GetRequiredService<IRepository<RealCalendar, int>>();
            var voteRepositoryFactory = serviceProvider.GetRequiredService<IFactory<IRepository<VotedRealPlayerWrapper, YearDayKey>>>();
            var voteRepository = voteRepositoryFactory.Create("official");
            var realTeamRepository = serviceProvider.GetRequiredService<IRepository<RealTeamWrapper, int>>();
            var allPlayersRepository = serviceProvider.GetRequiredService<IRepository<RealPlayersWrapper, int>>();
            for (var year = 1; year < 12; year++)
            {
                var calendar = await manager.Core.GetSerieACalendarAsync(year);
                if (calendar != null)
                {
                    var allPlayers = await manager.Core.GetAllPlayersAsync(year);
                    var playersWrapper = new RealPlayersWrapper
                    {
                        Players = new(),
                    };
                    foreach (var player in allPlayers)
                    {
                        playersWrapper.Players.Add(new RealPlayer
                        {
                            IsActive = true,
                            Name = player.Name,
                            RealTeam = new RealTeam { Name = player.RealTeam },
                            Role = (Role)(int)player.Role,
                        });
                    }
                    await allPlayersRepository.UpdateAsync(year, playersWrapper);
                    var realTeamWrapper = new RealTeamWrapper() { Teams = new() };
                    foreach (var team in calendar.Days[0].Games.Select(x => new string[] { x.Home, x.Away }).SelectMany(x => x))
                    {
                        realTeamWrapper.Teams.Add(new RealTeam
                        {
                            Name = team
                        });
                    }
                    await realTeamRepository.UpdateAsync(year, realTeamWrapper);
                    var realCalendar = new RealCalendar
                    {
                        Days = new(),
                        Year = year,
                    };
                    foreach (var day in calendar.Days)
                    {
                        realCalendar.Days.Add(new RealDay
                        {
                            SerieADay = day.SerieADay,
                            Year = year,
                            Games = day.Games.Select(x => new RealGame
                            {
                                Away = new RealTeam { Name = x.Away },
                                Home = new RealTeam
                                {
                                    Name = x.Home
                                },
                                AwayGoal = x.AwayGoal,
                                HomeGoal = x.HomeGoal,
                                Date = x.Date,
                                Delayed = x.Delayed
                            }).ToList()
                        });
                        var votes = await manager.Core.GetVotesAsync(year, day.SerieADay);
                        if (votes != null)
                        {
                            var newVotedRealPlayerWrapper = new VotedRealPlayerWrapper() { Players = new() };
                            foreach (var vote in votes)
                            {
                                var playerKey = new Player { Name = vote.Name }.Key;
                                newVotedRealPlayerWrapper.Players.Add(new VotedRealPlayer
                                {
                                    IsActive = true,
                                    Name = vote.Name,
                                    RealTeam = playersWrapper.Players.FirstOrDefault(x => x.Key == playerKey)?.RealTeam ?? new RealTeam { Name = "None" },
                                    Role = (Role)(int)vote.Role,
                                    Vote = new Vote
                                    {
                                        Assist = vote.Vote.Assist,
                                        IsOut = vote.Vote.IsOut,
                                        IsIn = vote.Vote.IsIn,
                                        IsFinal = vote.Vote.IsFinal,
                                        Penalty = vote.Vote.Penalty,
                                        Role = (Role)(int)vote.Role,
                                        Goal = vote.Vote.Goal,
                                        OwnGoal = vote.Vote.OwnGoal,
                                        Status = (Behaviour)(int)vote.Vote.Status,
                                        StoppedPenalty = vote.Vote.StoppedPenalty,
                                        SufferedGoal = vote.Vote.SufferedGoal,
                                        Value = vote.Vote.Value,
                                        WrongedPenalty = vote.Vote.WrongedPenalty,
                                        HasVote = vote.Vote.HasVote,
                                    }
                                });
                            }
                            await voteRepository.UpdateAsync(new YearDayKey
                            {
                                Day = day.SerieADay,
                                Year = year,
                            }, newVotedRealPlayerWrapper);
                        }
                    }
                    await calendarRepository.UpdateAsync(year, realCalendar);
                }
                Console.WriteLine($"Parsed serie a year: {year}");
            }
        }
        private static Dictionary<FantaSoccerRole, Role> s_allowedRoles = new Dictionary<FantaSoccerRole, Role>()
                                                    {
                                                        { FantaSoccerRole.BackupGoalKeeper,  Role.GoalKeeper },
                                                        { FantaSoccerRole.FirstBackupDefensor, Role.Defensor },
                                                        { FantaSoccerRole.SecondBackupDefensor, Role.Defensor },
                                                        { FantaSoccerRole.FirstBackupMidfielder, Role.Midfielder },
                                                        { FantaSoccerRole.SecondBackupMidfielder, Role.Midfielder },
                                                        { FantaSoccerRole.FirstBackupForward, Role.Forward },
                                                        { FantaSoccerRole.SecondBackupForward, Role.Forward },
                                                        { FantaSoccerRole.Forward, Role.Forward },
                                                        { FantaSoccerRole.Midfielder, Role.Midfielder },
                                                        { FantaSoccerRole.Defensor, Role.Defensor },
                                                        { FantaSoccerRole.GoalKeeper, Role.GoalKeeper },
                                                    };
        private static async Task ParseOldFormationsAndCalendarAsync(IServiceProvider serviceProvider, Manager manager, bool saveDailyTeam)
        {
            var groupRepository = serviceProvider.GetRequiredService<IRepository<Group, string>>();
            var currentGroup = await groupRepository.FirstOrDefaultAsync();
            var teamRepository = serviceProvider.GetRequiredService<IRepository<Team, TeamKey>>();
            var teamDayRepository = serviceProvider.GetRequiredService<IRepository<Team, TeamDayKey>>();
            var calendarRepository = serviceProvider.GetRequiredService<IRepository<Calendar, LeagueKey>>();
            var rankRepository = serviceProvider.GetRequiredService<IRepository<Rank, LeagueKey>>();
            var teamChecker = serviceProvider.GetRequiredService<ITeamChecker>();
            var groups = await manager.Group.ListAsync();
            var leagues = new List<string> { "Serie A", "Premier League", "Champions League", "Super League" };
            foreach (var groupId in groups)
            {
                for (var year = 1; year < 12; year++)
                {
                    foreach (var leagueId in leagues)
                    {
                        var leagueIdN = currentGroup.Value.Leagues.First(x => x.Name == leagueId).Id;
                        var newLeague = currentGroup.Value.Leagues.First(x => x.Name == leagueId);
                        var settings = currentGroup.Value.Leagues.First(x => x.Name == leagueId).Years.Find(x => x.Year == year)?.Settings;
                        if (settings == null)
                            continue;
                        var leagueKey = new LeagueKey
                        {
                            Group = currentGroup.Key,
                            League = leagueIdN,
                            Year = year,
                        };
                        var calendar = await manager.League.GetCalendarAsync(groupId, leagueId, year);
                        if (calendar != null)
                        {
                            var newCalendar = new Calendar
                            {
                                Rounds = new(),
                                Year = year
                            };
                            foreach (var day in calendar.Days)
                            {
                                foreach (var gameKeyValue in day.Games)
                                {
                                    var games = gameKeyValue.Value;
                                    var leu = gameKeyValue.Key.ToString();
                                    if (!newCalendar.Rounds.ContainsKey(leu))
                                    {
                                        newCalendar.Rounds.Add(leu, new());
                                    }
                                    var round = newCalendar.Rounds[leu];
                                    var currentDay = round.FirstOrDefault(x => x.SerieADay == day.SerieADay);
                                    if (currentDay == null)
                                    {
                                        currentDay = new Day
                                        {
                                            SerieADay = day.SerieADay,
                                            Games = new(),
                                            Number = day.SerieADay - settings.DelayedDay,
                                        };
                                        round.Add(currentDay);
                                    }
                                    var counter = 1;
                                    foreach (var game in games)
                                    {
                                        var homeOwner = CalculateOwner(game.HomeUsername);
                                        var awayOwner = CalculateOwner(game.AwayUsername);
                                        currentDay.Games.Add(new CalendarGame
                                        {
                                            Away = game.Away.Name,
                                            Home = game.Home.Name,
                                            AwayOwner = awayOwner,
                                            HomeOwner = homeOwner,
                                            Id = Guid.NewGuid().ToString(),
                                            Number = counter,
                                            Result = game.Result == null ? null : new GameResult
                                            {
                                                Away = new(game.Result.Away.Value, game.Result.Away.DefensiveBonus, game.Result.Away.OwnGoal),
                                                Home = new(game.Result.Home.Value, game.Result.Home.DefensiveBonus, game.Result.Home.OwnGoal),
                                                AwayGoal = game.Result.AwayGoal,
                                                HomeGoal = game.Result.HomeGoal,
                                                IsCancelled = false,
                                            }
                                        });
                                        if (newLeague.IsMain && saveDailyTeam)
                                        {
                                            await SaveAsync(game.HomeUsername, true);
                                            await SaveAsync(game.AwayUsername, false);

                                            async Task SaveAsync(string username, bool isInHome)
                                            {
                                                var dayTeam = await FantacalcioV4.Database.FantacalcioContext.GetAsync<FantacalcioV4.Team>($"Group/{groupId}/Year/{year}/User/{username}/{day.SerieADay}");
                                                var serieADay = day.SerieADay - 1;
                                                while ((dayTeam == null || dayTeam.Players.Count == 0) && serieADay >= 0)
                                                {
                                                    if (serieADay > 0)
                                                        dayTeam = await FantacalcioV4.Database.FantacalcioContext.GetAsync<FantacalcioV4.Team>($"Group/{groupId}/Year/{year}/User/{username}/{serieADay}");
                                                    else
                                                        dayTeam = await FantacalcioV4.Database.FantacalcioContext.GetAsync<FantacalcioV4.Team>($"Group/{groupId}/Year/{year}/User/{username}");
                                                    serieADay--;
                                                }
                                                var adder = 0;
                                                if (year == 3)
                                                {
                                                    foreach (var player in dayTeam.Players)
                                                    {
                                                        if (player.Position.X == 365)
                                                        {
                                                            if (player.Position.Y == 70 && player.Role == FantacalcioV4.Role.Defensor)
                                                            {
                                                                adder = 60;
                                                            }
                                                        }
                                                    }
                                                }
                                                if (year >= 10)
                                                {
                                                    foreach (var player in dayTeam.Players)
                                                    {
                                                        player.Position.Year = year;
                                                        player.Position.SerieADay = day.SerieADay;
                                                        player.Position.IsInHome = isInHome;
                                                        if (player.Position.X == -1)
                                                        {
                                                            player.Position.X = 1550;
                                                            player.Position.Y = 1550;
                                                        }
                                                        if (string.IsNullOrWhiteSpace(player.Name))
                                                        {
                                                            var name = player.Key.Split('/').Last();
                                                            name = name[..1].ToUpper() + name[1..].ToLower();
                                                            var roleKey = (FantaSoccerRole)(int)player.Position.Status;
                                                            var role = s_allowedRoles.ContainsKey(roleKey) ? (FantacalcioV4.Role)(int)s_allowedRoles[roleKey] : FantacalcioV4.Role.Defensor;
                                                            player.miniPlayer = new FantacalcioV4.RealPlayer(name, string.Empty, role, year);
                                                        }
                                                    }
                                                }
                                                if ((year == 5 && day.SerieADay >= 19) || year == 7)
                                                {
                                                    foreach (var player in dayTeam.Players)
                                                    {
                                                        player.Position.Year = year;
                                                        player.Position.SerieADay = day.SerieADay;
                                                        player.Position.IsInHome = isInHome;
                                                    }
                                                    if (dayTeam.Players.Count(x => x.Position.Status == FantacalcioV4.FantaSoccerRole.Tribune) > 10)
                                                    {
                                                        isInHome = !isInHome;
                                                        foreach (var player in dayTeam.Players)
                                                        {
                                                            player.Position.Year = year;
                                                            player.Position.SerieADay = day.SerieADay;
                                                            player.Position.IsInHome = isInHome;
                                                            player.Position.status = FantacalcioV4.FantaSoccerRole.All;
                                                        }
                                                    }
                                                }
                                                foreach (var player in dayTeam.Players)
                                                {
                                                    player.Position.Year = year;
                                                    player.Position.SerieADay = day.SerieADay;
                                                    player.Position.IsInHome = isInHome;
                                                    if (year == 1 && player.Name.ToLower() == "pereyra" && day.SerieADay < 25)
                                                    {
                                                        player.roleX = FantacalcioV4.Role.Defensor;
                                                    }
                                                    if (year == 1 && player.Name.ToLower() == "isla" && day.SerieADay < 25)
                                                    {
                                                        player.roleX = FantacalcioV4.Role.Midfielder;
                                                    }
                                                    if (year == 1 && player.Name.ToLower() == "camporese")
                                                    {
                                                        player.roleX = FantacalcioV4.Role.Defensor;
                                                    }
                                                    if (year == 1 && player.Name.ToLower() == "pereirinha" && day.SerieADay >= 25)
                                                    {
                                                        player.roleX = FantacalcioV4.Role.Midfielder;
                                                    }
                                                    if (year == 2 && player.Name.ToLower() == "canini")
                                                    {
                                                        player.roleX = FantacalcioV4.Role.Defensor;
                                                    }
                                                    if (year == 2 && player.Name.ToLower() == "aladje gomes")
                                                    {
                                                        player.roleX = FantacalcioV4.Role.Midfielder;
                                                    }
                                                    if (year == 2 && player.Name.ToLower() == "ariaudo")
                                                    {
                                                        player.roleX = FantacalcioV4.Role.Midfielder;
                                                    }
                                                    if (year == 2 && player.Name.ToLower() == "ljajic")
                                                    {
                                                        player.roleX = FantacalcioV4.Role.Midfielder;
                                                    }
                                                    if (year == 2 && player.Name.ToLower() == "basta")
                                                    {
                                                        player.roleX = FantacalcioV4.Role.Defensor;
                                                    }
                                                    if (year == 2 && player.Name.ToLower() == "bellomo")
                                                    {
                                                        player.roleX = FantacalcioV4.Role.Midfielder;
                                                    }
                                                    if (year == 2 && day.SerieADay >= 16 && player.Name.ToLower() == "mertens")
                                                    {
                                                        player.roleX = FantacalcioV4.Role.Forward;
                                                    }
                                                    if (year == 3 && player.Name.ToLower() == "salah")
                                                    {
                                                        player.roleX = FantacalcioV4.Role.Midfielder;
                                                    }
                                                    if (year == 2 && day.SerieADay >= 16 && player.Name.ToLower() == "peruzzi")
                                                    {
                                                        player.roleX = FantacalcioV4.Role.Defensor;
                                                    }
                                                    if (year == 3 && player.Name.ToLower() == "menez")
                                                    {
                                                        player.roleX = FantacalcioV4.Role.Midfielder;
                                                    }
                                                    if (year == 3 && player.Name.ToLower() == "antonelli")
                                                    {
                                                        player.roleX = FantacalcioV4.Role.Midfielder;
                                                    }
                                                    if (year == 3 && player.Name.ToLower() == "callejon")
                                                    {
                                                        player.roleX = FantacalcioV4.Role.Forward;
                                                    }
                                                    if (year == 4 && player.Name.ToLower() == "mertens")
                                                    {
                                                        player.roleX = FantacalcioV4.Role.Midfielder;
                                                    }
                                                    if (year == 4 && player.Name.ToLower() == "ilicic")
                                                    {
                                                        player.roleX = FantacalcioV4.Role.Forward;
                                                    }
                                                    if (year == 5 && day.SerieADay >= 22 && player.Name.ToLower() == "edenilson")
                                                    {
                                                        player.roleX = FantacalcioV4.Role.Midfielder;
                                                    }
                                                    if (year == 5 && day.SerieADay == 25 && player.Name.ToLower() == "asamoah")
                                                    {
                                                        player.roleX = FantacalcioV4.Role.Defensor;
                                                    }
                                                    if (year == 5 && day.SerieADay >= 27 && player.Name.ToLower() == "hiljemark")
                                                    {
                                                        player.roleX = FantacalcioV4.Role.Defensor;
                                                    }
                                                    if (year == 2 || year == 3 || year == 4 || (year == 5 && day.SerieADay < 19))
                                                    {
                                                        if (player.Position.X == 0 && player.Position.Y == 1800)
                                                        {
                                                            player.roleX = FantacalcioV4.Role.GoalKeeper;
                                                            player.GkChanged = true;
                                                            player.Position.Y = 70;
                                                            player.Position.X = 365;
                                                        }
                                                        else if (player.Position.X == 365)
                                                        {
                                                            player.Position.Y += adder;
                                                            switch (player.Position.Y)
                                                            {
                                                                case 70:
                                                                    player.roleX = FantacalcioV4.Role.GoalKeeper;
                                                                    break;
                                                                case 130:
                                                                    player.roleX = FantacalcioV4.Role.Defensor;
                                                                    break;
                                                                case 190:
                                                                    player.roleX = FantacalcioV4.Role.Defensor;
                                                                    break;
                                                                case 250:
                                                                    player.roleX = FantacalcioV4.Role.Midfielder;
                                                                    break;
                                                                case 310:
                                                                    player.roleX = FantacalcioV4.Role.Midfielder;
                                                                    break;
                                                                case 370:
                                                                    player.roleX = FantacalcioV4.Role.Forward;
                                                                    break;
                                                                case 410:
                                                                    player.roleX = FantacalcioV4.Role.Forward;
                                                                    break;
                                                                case 430:
                                                                    player.roleX = FantacalcioV4.Role.Forward;
                                                                    break;
                                                                default:
                                                                    player.Position.X = 1800;
                                                                    break;
                                                            }
                                                        }
                                                        else if (player.Position.Y <= 800)
                                                        {
                                                            //if (year == 3 && !isInHome)
                                                            //{
                                                            //    player.Position.Y = 700 - player.Position.Y;
                                                            //}
                                                            //if (player.Position.Y < 90)
                                                            //    player.roleX = FantacalcioV4.Role.GoalKeeper;
                                                            //else if (player.Position.Y < 300)
                                                            //    player.roleX = FantacalcioV4.Role.Defensor;
                                                            //else if (player.Position.Y < 600)
                                                            //    player.roleX = FantacalcioV4.Role.Midfielder;
                                                            //else
                                                            //    player.roleX = FantacalcioV4.Role.Forward;
                                                        }
                                                    }
                                                    player.Position.Role = player.Role;
                                                }
                                                if (year == 3 && dayTeam.Players.Count(x => x.Position.Status == FantacalcioV4.FantaSoccerRole.BackupGoalKeeper) >= 2)
                                                {
                                                    foreach (var player in dayTeam.Players)
                                                    {
                                                        if (player.GkChanged)
                                                        {
                                                            player.Position.status = FantacalcioV4.FantaSoccerRole.Tribune;
                                                        }
                                                    }
                                                }
                                                if ((year == 4 || year == 5) && dayTeam.Players.Count(x => x.Position.Status == FantacalcioV4.FantaSoccerRole.BackupGoalKeeper) >= 2)
                                                {
                                                    foreach (var gk in dayTeam.Players.Where(x => x.Position.Status == FantacalcioV4.FantaSoccerRole.BackupGoalKeeper).Skip(1))
                                                    {
                                                        gk.Position.status = FantacalcioV4.FantaSoccerRole.Tribune;
                                                    }
                                                }
                                                if (year == 5 && day.SerieADay >= 19)
                                                {
                                                    if (dayTeam.Players.Count(x => x.Position.Status == FantacalcioV4.FantaSoccerRole.SecondBackupDefensor) > 1)
                                                    {
                                                        dayTeam.Players.Where(x => x.Position.Status == FantacalcioV4.FantaSoccerRole.SecondBackupDefensor).First().Position.status = FantacalcioV4.FantaSoccerRole.FirstBackupDefensor;
                                                    }
                                                }
                                                var bitmap = new Bitmap(2500, 2500);
                                                foreach (var player in dayTeam.Players.OrderBy(x => x.Position.Status))
                                                {
                                                    Console.WriteLine($"{player.Name} with role: {player.Role}, pos:[{player.Position.X},{player.Position.Y}] has pos: {player.Position.Status}");
                                                    for (var i = player.Position.X; i < player.Position.X + 60; i++)
                                                    {
                                                        for (var j = player.Position.Y; j < player.Position.Y + 60; j++)
                                                        {
                                                            try
                                                            {
                                                                bitmap.SetPixel(j, i, GetColor());
                                                            }
                                                            catch { }
                                                            Color GetColor()
                                                            {
                                                                switch (player.Role)
                                                                {
                                                                    case FantacalcioV4.Role.Undefined:
                                                                        return Color.Black;
                                                                    case FantacalcioV4.Role.Forward:
                                                                        return Color.Red;
                                                                    case FantacalcioV4.Role.Midfielder:
                                                                        return Color.Yellow;
                                                                    case FantacalcioV4.Role.Defensor:
                                                                        return Color.Blue;
                                                                    case FantacalcioV4.Role.GoalKeeper:
                                                                        return Color.Violet;
                                                                }
                                                                return Color.Brown;
                                                            }
                                                        }
                                                    }
                                                }
                                                bitmap.Save(@"C:\Users\aless\Downloads\teamsdasdl.png");
                                                var owner = CalculateOwner(username);
                                                var newTeam = new Team
                                                {
                                                    Name = dayTeam.Name,
                                                    Owner = owner,
                                                    MoneyFromRank = dayTeam.MoneyFromRank,
                                                    RevenueMoney = dayTeam.RevenueMoney,
                                                    Players = dayTeam.Players.Select(x =>
                                                    {
                                                        var position = (FantaSoccerRole)(int)x.Position.Status;
                                                        return new Player()
                                                        {
                                                            Name = x.Name,
                                                            IsActive = true,
                                                            Position = position,
                                                            Price = x.Price,
                                                            RealTeam = new RealTeam { Name = x.RealTeam },
                                                            Role = (Role)(int)x.Role,
                                                            Status = (PlayerInTeamStatus)(int)x.TeamStatus
                                                        };
                                                    }).ToList()
                                                };
                                                if (newTeam.Players.Count < 25)
                                                {
                                                    var oldTeam = await FantacalcioV4.Database.FantacalcioContext.GetAsync<FantacalcioV4.Team>($"Group/{groupId}/Year/{year}/User/{username}");
                                                    var oldPlayers = oldTeam.Players.Select(x => new Player()
                                                    {
                                                        Name = x.Name,
                                                        IsActive = true,
                                                        Position = FantaSoccerRole.Tribune,
                                                        Price = x.Price,
                                                        RealTeam = new RealTeam { Name = x.RealTeam },
                                                        Role = (Role)(int)x.Role,
                                                        Status = (PlayerInTeamStatus)(int)x.TeamStatus
                                                    }).ToList();
                                                    foreach (var oldPlayer in oldPlayers.Where(x => x.Status == PlayerInTeamStatus.Active))
                                                    {
                                                        if (!newTeam.Players.Exists(x => x.Key == oldPlayer.Key))
                                                        {
                                                            newTeam.Players.Add(oldPlayer);
                                                        }
                                                        if (newTeam.Players.Count >= 25)
                                                            break;
                                                    }
                                                    if (newTeam.Players.Count < 25)
                                                    {
                                                        foreach (var oldPlayer in oldPlayers.Where(x => x.Status != PlayerInTeamStatus.Active))
                                                        {
                                                            if (!newTeam.Players.Exists(x => x.Key == oldPlayer.Key))
                                                            {
                                                                newTeam.Players.Add(oldPlayer);
                                                            }
                                                            if (newTeam.Players.Count >= 25)
                                                                break;
                                                        }
                                                    }
                                                }
                                                newTeam.Players = newTeam.Players.OrderBy(x => x.Position).ToList();
                                                foreach (var player in newTeam.Players)
                                                {
                                                    Console.WriteLine($"{player.Name} with role: {player.Role}, has pos: {player.Position}");
                                                }
                                                var validation = teamChecker.Validate(newTeam);
                                                if (!validation.IsOk && year == 5 && day.SerieADay >= 29)
                                                {
                                                    DoIt(Role.Forward, FantaSoccerRole.FirstBackupForward, FantaSoccerRole.SecondBackupForward);
                                                    DoIt(Role.Midfielder, FantaSoccerRole.FirstBackupMidfielder, FantaSoccerRole.SecondBackupMidfielder);
                                                    DoIt(Role.Defensor, FantaSoccerRole.FirstBackupDefensor, FantaSoccerRole.SecondBackupDefensor);
                                                    void DoIt(Role role, FantaSoccerRole fRole, FantaSoccerRole fRoleN)
                                                    {
                                                        if (newTeam.Players.Count(x => x.Role == role && x.Position == fRole) > 1)
                                                        {
                                                            newTeam.Players.Where(x => x.Role == role && x.Position == fRole).Skip(1).First().Position = fRoleN;
                                                        }
                                                    }
                                                    validation = teamChecker.Validate(newTeam);
                                                }
                                                if (!validation.IsOk && year >= 6)
                                                {
                                                    DoIt(Role.Forward, FantaSoccerRole.FirstBackupForward, FantaSoccerRole.SecondBackupForward);
                                                    DoIt(Role.Midfielder, FantaSoccerRole.FirstBackupMidfielder, FantaSoccerRole.SecondBackupMidfielder);
                                                    DoIt(Role.Defensor, FantaSoccerRole.FirstBackupDefensor, FantaSoccerRole.SecondBackupDefensor);
                                                    DoIt(Role.Forward, FantaSoccerRole.SecondBackupForward, FantaSoccerRole.FirstBackupForward);
                                                    DoIt(Role.Midfielder, FantaSoccerRole.SecondBackupMidfielder, FantaSoccerRole.FirstBackupMidfielder);
                                                    DoIt(Role.Defensor, FantaSoccerRole.SecondBackupDefensor, FantaSoccerRole.FirstBackupDefensor);
                                                    DoIt2(Role.Forward, FantaSoccerRole.FirstBackupForward, FantaSoccerRole.SecondBackupForward);
                                                    DoIt2(Role.Midfielder, FantaSoccerRole.FirstBackupMidfielder, FantaSoccerRole.SecondBackupMidfielder);
                                                    DoIt2(Role.Defensor, FantaSoccerRole.FirstBackupDefensor, FantaSoccerRole.SecondBackupDefensor);
                                                    DoIt2(Role.Forward, FantaSoccerRole.SecondBackupForward, FantaSoccerRole.FirstBackupForward);
                                                    DoIt2(Role.Midfielder, FantaSoccerRole.SecondBackupMidfielder, FantaSoccerRole.FirstBackupMidfielder);
                                                    DoIt2(Role.Defensor, FantaSoccerRole.SecondBackupDefensor, FantaSoccerRole.FirstBackupDefensor);
                                                    DoIt2(Role.GoalKeeper, FantaSoccerRole.BackupGoalKeeper, FantaSoccerRole.BackupGoalKeeper);
                                                    void DoIt(Role role, FantaSoccerRole fRole, FantaSoccerRole fRoleN)
                                                    {
                                                        if (newTeam.Players.Count(x => x.Role == role && x.Position == fRole) > 1)
                                                        {
                                                            newTeam.Players.Where(x => x.Role == role && x.Position == fRole).Skip(1).First().Position = fRoleN;
                                                        }
                                                        if (newTeam.Players.Count(x => x.Role == role && x.Position == fRole) == 0)
                                                        {
                                                            newTeam.Players.Where(x => x.Role == role && x.Position == FantaSoccerRole.Tribune).First().Position = fRoleN;
                                                        }
                                                    }
                                                    void DoIt2(Role role, FantaSoccerRole fRole, FantaSoccerRole fRoleN)
                                                    {
                                                        if (newTeam.Players.Count(x => x.Role == role && x.Position == fRole) > 1)
                                                        {
                                                            foreach (var pl in newTeam.Players.Where(x => x.Role == role && x.Position == fRole).Skip(1))
                                                                pl.Position = FantaSoccerRole.Tribune;
                                                        }
                                                    }
                                                    validation = teamChecker.Validate(newTeam);
                                                }
                                                if (!validation.IsOk && year == 7)
                                                {
                                                    foreach (var player in newTeam.Players.Where(x => x.Position <= FantaSoccerRole.Forward))
                                                    {
                                                        var role = s_allowedRoles[player.Position];
                                                        player.Role = role;
                                                    }
                                                    validation = teamChecker.Validate(newTeam);
                                                }
                                                if (!validation.IsOk && year == 5)
                                                {
                                                    var allowedRoles = new Dictionary<FantaSoccerRole, Role>()
                                                    {
                                                        { FantaSoccerRole.BackupGoalKeeper,  Role.GoalKeeper },
                                                        { FantaSoccerRole.FirstBackupDefensor, Role.Defensor },
                                                        { FantaSoccerRole.SecondBackupDefensor, Role.Defensor },
                                                        { FantaSoccerRole.FirstBackupMidfielder, Role.Midfielder },
                                                        { FantaSoccerRole.SecondBackupMidfielder, Role.Midfielder },
                                                        { FantaSoccerRole.FirstBackupForward, Role.Forward },
                                                        { FantaSoccerRole.SecondBackupForward, Role.Forward },
                                                        { FantaSoccerRole.Forward, Role.Forward },
                                                        { FantaSoccerRole.Midfielder, Role.Midfielder },
                                                        { FantaSoccerRole.Defensor, Role.Defensor },
                                                        { FantaSoccerRole.GoalKeeper, Role.GoalKeeper },
                                                    };
                                                    foreach (var player in newTeam.Players.Where(x => x.Position <= FantaSoccerRole.Forward))
                                                    {
                                                        var role = allowedRoles[player.Position];
                                                        player.Role = role;
                                                    }
                                                    List<Player> wrongPositions = new();
                                                    foreach (var player in newTeam.Players.Where(x => x.Position >= FantaSoccerRole.BackupGoalKeeper && x.Position <= FantaSoccerRole.SecondBackupForward))
                                                    {
                                                        if (allowedRoles[player.Position] != player.Role)
                                                        {
                                                            wrongPositions.Add(player);
                                                        }
                                                    }
                                                    if (wrongPositions.Count == 1)
                                                    {
                                                        var role = allowedRoles[wrongPositions[0].Position];
                                                        var tribunePlayer = newTeam.Players.FirstOrDefault(x => x.Role == role && x.Position == FantaSoccerRole.Tribune);
                                                        if (tribunePlayer != null)
                                                        {
                                                            tribunePlayer.Position = wrongPositions[0].Position;
                                                            wrongPositions[0].Position = FantaSoccerRole.Tribune;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        for (var i = 0; i < wrongPositions.Count; i++)
                                                        {
                                                            var player = wrongPositions[i];
                                                            if (allowedRoles[player.Position] != player.Role)
                                                            {
                                                                for (var j = i + 1; j < wrongPositions.Count; j++)
                                                                {
                                                                    var playerC = wrongPositions[j];
                                                                    if (allowedRoles[playerC.Position] == player.Role
                                                                        && allowedRoles[player.Position] == playerC.Role)
                                                                    {
                                                                        var q = playerC.Position;
                                                                        playerC.Position = player.Position;
                                                                        player.Position = q;
                                                                        break;
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                    validation = teamChecker.Validate(newTeam);
                                                }
                                                if (validation.IsOk || (validation.Message == "Devono esserci sette giocatori in tribuna." && year >= 8) || (year >= 9 && dayTeam.Players.Count == 0))
                                                {
                                                    if ((year >= 9 && dayTeam.Players.Count == 0))
                                                    {
                                                        var cancelledDay = day.SerieADay - settings.DelayedDay;
                                                        if (settings.CancelledDay != cancelledDay)
                                                        {
                                                            settings.CancelledDay = cancelledDay;
                                                            await groupRepository.UpdateAsync(currentGroup.Key, currentGroup.Value);
                                                        }
                                                    }
                                                    var dayTeamKey = new TeamDayKey
                                                    {
                                                        Email = owner,
                                                        Day = day.SerieADay,
                                                        Group = currentGroup.Key,
                                                        Year = year,
                                                        BasketId = newLeague.BasketsId.First()
                                                    };
                                                    await teamDayRepository.UpdateAsync(dayTeamKey, newTeam);
                                                }
                                                else
                                                {
                                                    var dayTeamKey = new TeamDayKey
                                                    {
                                                        Email = owner,
                                                        Day = day.SerieADay,
                                                        Group = currentGroup.Key,
                                                        Year = year,
                                                        BasketId = newLeague.BasketsId.First()
                                                    };
                                                    await teamDayRepository.UpdateAsync(dayTeamKey, newTeam);
                                                }
                                            }

                                        }
                                        counter++;
                                    }
                                }
                                Console.WriteLine($"parsed {groupId} - {leagueId} - {day.SerieADay}");
                            }
                            await calendarRepository.UpdateAsync(leagueKey, newCalendar);
                        }
                        var oldLeague = await manager.League.GetAsync(groupId, leagueId, year);

                        foreach (var team in oldLeague.Teams)
                        {
                            var owner = CalculateOwner(team.Username);
                            var newTeam = new Team()
                            {
                                Name = team.Name,
                                Owner = owner,
                                Players = new(),
                                MoneyFromRank = team.MoneyFromRank,
                                RevenueMoney = team.RevenueMoney,
                            };
                            var oldTeam = await FantacalcioV4.Database.FantacalcioContext.GetAsync<FantacalcioV4.Team>($"Group/{groupId}/Year/{year}/User/{team.Username}");
                            var oldPlayers = oldTeam.Players.Select(x => new Player()
                            {
                                Name = x.Name,
                                IsActive = true,
                                Position = FantaSoccerRole.Tribune,
                                Price = x.Price,
                                RealTeam = new RealTeam { Name = x.RealTeam },
                                Role = (Role)(int)x.Role,
                                Status = (PlayerInTeamStatus)(int)x.TeamStatus
                            }).ToList();
                            foreach (var player in oldPlayers)
                            {
                                newTeam.Players.Add(player);
                            }
                            await teamRepository.UpdateAsync(new TeamKey
                            {
                                Email = owner,
                                Group = currentGroup.Value.Id,
                                Year = year,
                                BasketId = newLeague.BasketsId.First()
                            }, newTeam);
                            Console.WriteLine($"save team {groupId} - {team.Username} - {owner}");
                        }
                    }
                }
            }
        }
        private static readonly Dictionary<string, string> s_mappedUsers = new()
        {
                { "alessandrof", "al.forbicioni@gmail.com|||Serie A" },
                { "Keysersoze", "alessandro.rapiti44@gmail.com|||Serie A" },
                { "Oliver", "alessiooliverio@gmail.com|||Serie A" },
                { "luca", "baghiniluca@gmail.com|||Serie A" },
                { "DarkLord", "christian.lucaccioni@outlook.it|||Serie A" },
                { "Henrich", "crostafabio87@gmail.com|||Premier League" },
                { "Squall", "daniele.fetoni@hotmail.it|||Serie A" },
                { "jeffturner", "federicomartelloni@gmail.com|||Premier League" },
                { "ElettroAle", "fiani.alessandro@gmail.com|||Premier League" },
                { "Champion", "filippo.bossi.01@gmail.com|||Premier League" },
                { "Goldenhot", "goldenhot@hotmail.it|||Serie A" },
                { "saretta110", "joria87@hotmail.it|||Premier League" },
                { "SirJem", "luca.morleschi@outlook.it|||Premier League" },
                { "Maurizio", "m.forbicioni@hotmail.it|||Serie A" },
                { "perugini", "peruginialessandro@hotmail.it|||Premier League" },
                { "scarsenal", "theos.lumia@hotmail.it|||Serie A" },
                { "lorenzo", "lorenzo.catarcia@gmail.com|||Premier League" },
                { "marcog", "graziottimarco@gmail.com|||Premier League" },
                { "AndreaRules", "nicosia.andrea99@gmail.com|||Premier League" },
                { "Snailman", "Snail84muz@hotmail.com|||Serie A" },
            };
        private static string CalculateOwner(string username)
        {
            string? owner = null;
            if (s_mappedUsers.TryGetValue(username, out var value))
            {
                var informationForUser = value.Split("|||");
                owner = informationForUser[0];
            }
            return owner ?? username;
        }
        private static async Task AdjustLeagueAsync(IServiceProvider serviceProvider)
        {
            var teamCalculator = serviceProvider.GetRequiredService<ITeamCalculator>();
            var rankCalculator = serviceProvider.GetRequiredService<IRankCalculator>();
            var groupRepository = serviceProvider.GetRequiredService<IRepository<Group, string>>();
            var teamDayRepository = serviceProvider.GetRequiredService<IRepository<Team, TeamDayKey>>();
            var calendarRepository = serviceProvider.GetRequiredService<IRepository<Calendar, LeagueKey>>();
            var rankRepository = serviceProvider.GetRequiredService<IRepository<Rank, LeagueKey>>();
            var groups = await groupRepository.ToListAsync();
            var group = groups.First();
            foreach (var league in group.Value.Leagues.Where(x => x.Type == LeagueType.Cup))
            {
                foreach (var yearlyLeague in league.Years)
                {
                    Console.WriteLine($"Year {yearlyLeague.Year}");
                    var leagueKey = new LeagueKey
                    {
                        Group = group.Key,
                        League = league.Id,
                        Year = yearlyLeague.Year,
                    };
                    var calendar = await calendarRepository.GetAsync(leagueKey);
                    if (calendar != null)
                    {
                        if (calendar.Rounds.ContainsKey("0"))
                            calendar.Rounds.Remove("0");
                        List<Day> finals = new();
                        foreach (var round in calendar.Rounds)
                        {
                            var dayForFinals = round.Value.Where(x => x.Number > 12).ToList();
                            foreach (var day in dayForFinals)
                                round.Value.Remove(day);
                            finals.AddRange(dayForFinals);
                            Console.WriteLine(round.Key);
                            foreach (var day in round.Value)
                            {
                                Console.WriteLine($"{day.Number} ({day.SerieADay})");
                                foreach (var game in day.Games)
                                {
                                    Console.WriteLine($"{game.Home}-{game.Away} {game.Result.HomeGoal}-{game.Result.AwayGoal}");
                                }
                            }
                            Console.WriteLine();
                            Console.WriteLine();
                            Console.WriteLine();
                        }
                        List<Day> realFinals = new();
                        var numberDay = 13;
                        foreach (var dayInFinals in finals.OrderBy(x => x.Number).GroupBy(x => x.Number))
                        {
                            var day = new Day() { Number = numberDay, Games = dayInFinals.SelectMany(x => x.Games).GroupBy(x => $"{x.HomeOwner}-{x.AwayOwner}").Select(x => x.First()).ToList(), SerieADay = dayInFinals.First().SerieADay };
                            realFinals.Add(day);
                            numberDay++;
                        }
                        calendar.Rounds.Add("Finals", realFinals);
                        foreach (var day in calendar.Rounds["Finals"])
                        {
                            Console.WriteLine($"{day.Number} ({day.SerieADay})");
                            foreach (var game in day.Games)
                            {
                                Console.WriteLine($"{game.Home}-{game.Away} {game.Result.HomeGoal}-{game.Result.AwayGoal} ({game.Result.Home.Value}-{game.Result.Away.Value})");
                            }
                        }
                        await calendarRepository.UpdateAsync(leagueKey, calendar);
                    }
                    Console.WriteLine();
                    Console.WriteLine();
                    Console.WriteLine();
                    Console.WriteLine();
                    Console.WriteLine();
                    Console.WriteLine();
                }
            }
        }
        private static async Task CreateCalendarAndRankAsync(IServiceProvider serviceProvider)
        {
            var teamCalculator = serviceProvider.GetRequiredService<ITeamCalculator>();
            var rankCalculator = serviceProvider.GetRequiredService<IRankCalculator>();
            var groupRepository = serviceProvider.GetRequiredService<IRepository<Group, string>>();
            var teamDayRepository = serviceProvider.GetRequiredService<IRepository<Team, TeamDayKey>>();
            var calendarRepository = serviceProvider.GetRequiredService<IRepository<Calendar, LeagueKey>>();
            var rankRepository = serviceProvider.GetRequiredService<IRepository<Rank, LeagueKey>>();
            var groups = await groupRepository.ToListAsync();
            foreach (var group in groups)
            {
                var baskets = group.Value.Baskets;
                foreach (var league in group.Value.Leagues)
                {
                    foreach (var yearlyLeague in league.Years)
                    {
                        if (yearlyLeague.Year >= 12)
                            continue;
                        var leagueKey = new LeagueKey
                        {
                            Group = group.Key,
                            League = league.Id,
                            Year = yearlyLeague.Year,
                        };
                        Console.WriteLine();
                        Console.WriteLine();
                        Console.WriteLine();
                        Console.WriteLine();
                        Console.WriteLine($"Calendar saved {group.Value.Name} - {league.Name} - {leagueKey.Year}");
                        var calendar = await calendarRepository.GetAsync(leagueKey);
                        //var needsRecalculation = calendar != null && calendar.Rounds.SelectMany(x => x.Value.SelectMany(x => x.Games)).Count(x => x.Result == null || (x.Result.HomeGoal == 0 && x.Result.AwayGoal == 0)) > 40;
                        var needsRecalculation = calendar != null && yearlyLeague.Year > 15;
                        var differences = 0;
                        if (needsRecalculation)
                        {
                            var breakThePoint = false;
                            var onlyCounter = true;
                            foreach (var round in calendar.Rounds)
                            {
                                var isImpossibleThatIsDeleted = false;
                                var cancelled = 0;
                                foreach (var day in round.Value)
                                {
                                    Console.WriteLine();
                                    Console.WriteLine($"{day.Number} [{day.SerieADay}]");
                                    var isCancelled = !isImpossibleThatIsDeleted && day.Games.All(x => (x.Result?.Home?.Value ?? 0) == 0);
                                    if (day.Number <= yearlyLeague.Settings.CancelledDay || isCancelled)
                                    {
                                        cancelled++;
                                        foreach (var game in day.Games)
                                        {
                                            if (game.Result != null)
                                                game.Result.IsCancelled = true;
                                            else
                                                game.Result = new GameResult { IsCancelled = true };
                                            Console.WriteLine($"{game.Home}-{game.Away} isCancelled:{game.Result.IsCancelled}");
                                        }
                                        yearlyLeague.Settings.CancelledDay = cancelled;
                                        await groupRepository.UpdateAsync(group.Key, group.Value);
                                    }
                                    else
                                    {
                                        isImpossibleThatIsDeleted = true;
                                        await teamCalculator.BuildRequestAsync(yearlyLeague.Year, day.SerieADay, yearlyLeague.Settings, false, true, false);
                                        foreach (var game in day.Games)
                                        {
                                            var homeTeamKey = new TeamDayKey
                                            {
                                                Year = yearlyLeague.Year,
                                                Group = group.Key,
                                                Day = day.SerieADay,
                                                BasketId = GetBasketId(game.HomeOwner),
                                                Email = game.HomeOwner
                                            };
                                            var homeTeam = await teamDayRepository.GetAsync(homeTeamKey);
                                            if (homeTeam == null)
                                            {
                                                var previousKeyTeam = new TeamDayKey
                                                {
                                                    Year = yearlyLeague.Year,
                                                    Group = group.Key,
                                                    Day = day.SerieADay - 1,
                                                    BasketId = GetBasketId(game.HomeOwner),
                                                    Email = game.HomeOwner
                                                };
                                                homeTeam = await teamDayRepository.GetAsync(previousKeyTeam);
                                                await teamDayRepository.UpdateAsync(homeTeamKey, homeTeam);
                                            }
                                            var awayTeamKey = new TeamDayKey
                                            {
                                                Year = yearlyLeague.Year,
                                                Group = group.Key,
                                                Day = day.SerieADay,
                                                BasketId = GetBasketId(game.AwayOwner),
                                                Email = game.AwayOwner
                                            };
                                            var awayTeam = await teamDayRepository.GetAsync(awayTeamKey);
                                            if (awayTeam == null)
                                            {
                                                var previousKeyTeam = new TeamDayKey
                                                {
                                                    Year = yearlyLeague.Year,
                                                    Group = group.Key,
                                                    Day = day.SerieADay - 1,
                                                    BasketId = GetBasketId(game.AwayOwner),
                                                    Email = game.AwayOwner
                                                };
                                                awayTeam = await teamDayRepository.GetAsync(previousKeyTeam);
                                                await teamDayRepository.UpdateAsync(awayTeamKey, awayTeam);
                                            }
                                            var homePoint = teamCalculator.CalculatePoint(homeTeam.ActivePlayers);
                                            var awayPoint = teamCalculator.CalculatePoint(awayTeam.ActivePlayers);
                                            var result = new GameResult
                                            {
                                                Home = homePoint,
                                                Away = awayPoint,
                                            };
                                            result.CalculateGoals(yearlyLeague.Settings);
                                            if (game.Result?.Home?.Value != homePoint.Value || game.Result?.Away?.Value != awayPoint.Value)
                                                differences++;
                                            if (!onlyCounter)
                                            {
                                                Console.WriteLine($"{game.Home}-{game.Away} {game.Result?.HomeGoal}-{game.Result?.AwayGoal} [{game.Result?.Home?.Value}-{game.Result?.Away?.Value}]");
                                                Console.WriteLine($"{game.Home}-{game.Away} {result.HomeGoal}-{result.AwayGoal} [{result.Home.Value}-{result.Away.Value}]");
                                                if (game.Result?.Home?.Value != homePoint.Value || game.Result?.Away?.Value != awayPoint.Value)
                                                {
                                                    var sum = 0f;
                                                    foreach (var player in teamCalculator.GetRightFormation(homeTeam.ActivePlayers).OrderBy(x => x.CurrentPosition))
                                                    {
                                                        Console.WriteLine($"{player.Current.Name} - {player.FinalValue?.Value} ({player.Current.Position})[{player.CurrentPosition}]");
                                                        if (player.CurrentPosition <= FantaSoccerRole.Forward && player.FinalValue != null)
                                                            sum += player.FinalValue.Value;
                                                    }
                                                    Console.WriteLine(sum);
                                                    var sum1 = 0f;
                                                    foreach (var player in teamCalculator.GetRightFormation(awayTeam.ActivePlayers).OrderBy(x => x.CurrentPosition))
                                                    {
                                                        Console.WriteLine($"{player.Current.Name} - {player.FinalValue?.Value} ({player.Current.Position})[{player.CurrentPosition}]");
                                                        if (player.CurrentPosition <= FantaSoccerRole.Forward && player.FinalValue != null)
                                                            sum1 += player.FinalValue.Value;
                                                    }
                                                    Console.WriteLine(sum1);
                                                    Console.WriteLine($"[{(game.Result.Home.Value == homePoint.Value)}]({sum})[{game.Result.Home.Value}] {homePoint.Value} - {awayPoint.Value} [{game.Result.Away.Value}]({sum1})[{(game.Result.Away.Value == awayPoint.Value)}]");
                                                    if (breakThePoint)
                                                    {
                                                        var q = Console.ReadLine();
                                                        if (!string.IsNullOrWhiteSpace(q))
                                                            breakThePoint = false;
                                                    }
                                                    if (game.Result.Home.Value != homePoint.Value)
                                                    {
                                                        homePoint = teamCalculator.CalculatePoint(homeTeam.ActivePlayers);
                                                    }
                                                    if (game.Result.Away.Value != awayPoint.Value)
                                                    {
                                                        awayPoint = teamCalculator.CalculatePoint(awayTeam.ActivePlayers);
                                                    }
                                                }
                                            }
                                            game.Result = result;
                                            if (!onlyCounter)
                                            {
                                                Console.WriteLine();
                                                Console.WriteLine();
                                                Console.WriteLine();
                                            }
                                            string GetBasketId(string owner)
                                            {
                                                foreach (var basketId in league.BasketsId)
                                                {
                                                    var basket = baskets.FirstOrDefault(x => x.Id == basketId && x.Years.Any(t => t.Teams.Any(q => q.Owner == owner)));
                                                    if (basket != null)
                                                        return basket.Id;
                                                }
                                                return league.BasketsId.First();
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        if (yearlyLeague.Year == 6 && (league.Name == "Serie A" || league.Name == "Super League"))
                        {
                            var points = 0;
                            foreach (var round in calendar.Rounds)
                            {
                                foreach (var day in round.Value)
                                {
                                    foreach (var game in day.Games)
                                    {
                                        if ((game.HomeOwner == "alessandro.rapiti44@gmail.com" && game.Result.Value == GameResultType.AwayWon))
                                        {
                                            points += 3;
                                            game.Result = new GameResult
                                            {
                                                Away = new Fantasoccer.Point(game.Result.Away.Value, false, false),
                                                Home = new Fantasoccer.Point(game.Result.Away.Value + 6, false, false),
                                                AwayGoal = game.Result.AwayGoal,
                                                HomeGoal = game.Result.AwayGoal + 1,
                                            };
                                        }
                                        if ((game.AwayOwner == "alessandro.rapiti44@gmail.com" && game.Result.Value == GameResultType.HomeWon))
                                        {
                                            points += 3;
                                            game.Result = new GameResult
                                            {
                                                Away = new Fantasoccer.Point(game.Result.Home.Value + 6, false, false),
                                                Home = new Fantasoccer.Point(game.Result.Home.Value, false, false),
                                                AwayGoal = game.Result.HomeGoal + 1,
                                                HomeGoal = game.Result.HomeGoal,
                                            };
                                        }
                                        if (points > 26)
                                            goto here;
                                    }
                                }
                            }
                        }
                        if (yearlyLeague.Year == 7 && league.Name == "Serie A")
                        {
                            var points = 0;
                            foreach (var round in calendar.Rounds)
                            {
                                foreach (var day in round.Value)
                                {
                                    foreach (var game in day.Games)
                                    {
                                        if ((game.HomeOwner == "christian.lucaccioni@outlook.it" && game.Result.Value == GameResultType.AwayWon))
                                        {
                                            points += 3;
                                            game.Result = new GameResult
                                            {
                                                Away = new Fantasoccer.Point(game.Result.Away.Value, false, false),
                                                Home = new Fantasoccer.Point(game.Result.Away.Value + 6, false, false),
                                                AwayGoal = game.Result.AwayGoal,
                                                HomeGoal = game.Result.AwayGoal + 1,
                                            };
                                        }
                                        if ((game.AwayOwner == "christian.lucaccioni@outlook.it" && game.Result.Value == GameResultType.HomeWon))
                                        {
                                            points += 3;
                                            game.Result = new GameResult
                                            {
                                                Away = new Fantasoccer.Point(game.Result.Home.Value + 6, false, false),
                                                Home = new Fantasoccer.Point(game.Result.Home.Value, false, false),
                                                AwayGoal = game.Result.HomeGoal + 1,
                                                HomeGoal = game.Result.HomeGoal,
                                            };
                                        }
                                        if (points > 9)
                                            goto here;
                                    }
                                }
                            }
                        }
here: var rank = rankCalculator.GetRank(yearlyLeague.Settings, calendar, league.Type);
                        await rankRepository.UpdateAsync(leagueKey, rank);
                        Console.WriteLine();
                        Console.WriteLine();
                        Console.WriteLine($"the differences are: {differences}.");
                        Console.WriteLine();
                        Console.WriteLine();
                        Console.WriteLine($"Rank saved {group.Value.Name} - {league.Name} - {leagueKey.Year}");
                        foreach (var d in rank.RankedTeams)
                        {
                            Console.WriteLine($"{d.Key}");
                            foreach (var t in d.Value.OrderByDescending(x => x.Point))
                            {
                                Console.WriteLine($"{t.Name} - {t.Point} - {t.Goal}");
                            }
                        }
                        //foreach (var t in rank.RankedTeams)
                        //{
                        //    Console.WriteLine($"Liga {league.Name} ({yearlyLeague.Year}) [{t.Key}]");
                        //    foreach (var q in t.Value)
                        //    {
                        //        Console.WriteLine($"{q.Name}-{q.Point}");
                        //    }
                        //}
                    }
                }
            }
        }
    }
}


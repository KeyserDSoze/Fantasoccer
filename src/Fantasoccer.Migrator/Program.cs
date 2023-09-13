// See https://aka.ms/new-console-template for more information
using FantacalcioV4.ApiManager;
using Fantasoccer;
using Fantasoccer.Business;
using Fantasoccer.InMemory;
using Mapster;
using Microsoft.Extensions.DependencyInjection;
using Rystem;
using Fantasoccer.Infrastructure;
using RepositoryFramework;
using System.Drawing;

namespace Migrator
{
    public static class EntryPoint
    {
        public static async Task Main(string[] args)
        {
            var services = new ServiceCollection();
            var storageAccountName = "superfc";
            var storageAccountKey = "CS124aZwZywrMrxFiCdGFipSN+UbExX1BPEb/9aY1dWGLdSboO2drxh3yFSFOVM6uGc7Grahs/sZcpDRtP77yg==";
            var newStorage = "DefaultEndpointsProtocol=https;AccountName=calcutta2309;AccountKey=sA2ZYWk/SJJlBEac/5EhBgD2/A1sSqyyc5lmf0Xj587XJMgunWe4oXfzLq19TspgjUqfiuNT76SB+ASt7FG4TQ==;EndpointSuffix=core.windows.net";
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
            await ParseOldFormationsAndCalendarAsync(serviceProvider, manager);
        }
        private static async Task CreateGroupAndMapUsersAsync(IServiceProvider serviceProvider, Manager manager)
        {
            var groupRepository = serviceProvider.GetRequiredService<IRepository<Group, string>>();
            var userRepository = serviceProvider.GetRequiredService<IRepository<AppIdentity, string>>();
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
                    }
                }
                foreach (var userX in await userRepository.ToListAsync())
                {
                    var user = userX.Value;
                    if (user.Email == null)
                    {
                        user.Email = userX.Key;
                        user.Username = userX.Key;
                        user.Groups = new();
                    }
                    group.Users.Add(new UserOfAGroup
                    {
                        Username = user.Username,
                        Email = user.Email,
                        Role = user.Email == "alessandro.rapiti44@gmail.com" ? IdentityRole.Admin | IdentityRole.Participant : IdentityRole.Participant
                    });
                    user.Groups.Clear();
                    user.Groups.Add(group.ToGroupIdentity(user.Email));
                    await userRepository.UpdateAsync(user.Email, user);
                }
                if (!await groupRepository.ExistAsync(group.Id))
                    await groupRepository.InsertAsync(group.Id, group);
                else
                    await groupRepository.UpdateAsync(group.Id, group);
            }
        }
        private static async Task ParseSerieAVotesAndCalendar(IServiceProvider serviceProvider, Manager manager)
        {
            var calendarRepository = serviceProvider.GetRequiredService<IRepository<RealCalendar, int>>();
            var voteRepositoryFactory = serviceProvider.GetRequiredService<IFactory<IRepository<VotedRealPlayerWrapper, YearDayKey>>>();
            var voteRepository = voteRepositoryFactory.Create("official");
            var realTeamRepository = serviceProvider.GetRequiredService<IRepository<RealTeamWrapper, int>>();
            var allPlayersRepository = serviceProvider.GetRequiredService<IRepository<RealPlayersWrapper, int>>();
            for (var year = 1; year < 11; year++)
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
            }
        }
        private static async Task ParseOldFormationsAndCalendarAsync(IServiceProvider serviceProvider, Manager manager)
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
                for (var year = 2; year < 12; year++)
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
                            League = leagueIdN
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
                                            Result = new GameResult
                                            {
                                                Away = new(game.Result.Away.Value, game.Result.Away.DefensiveBonus, game.Result.Away.OwnGoal),
                                                Home = new(game.Result.Home.Value, game.Result.Home.DefensiveBonus, game.Result.Home.OwnGoal),
                                                AwayGoal = game.Result.AwayGoal,
                                                HomeGoal = game.Result.HomeGoal,
                                                IsCancelled = false,
                                            }
                                        });
                                        if (newLeague.IsMain)
                                        {
                                            await SaveAsync(game.HomeUsername, true);
                                            await SaveAsync(game.AwayUsername, false);

                                            async Task SaveAsync(string username, bool isInHome)
                                            {
                                                var dayTeam = await FantacalcioV4.Database.FantacalcioContext.GetAsync<FantacalcioV4.Team>($"Group/{groupId}/Year/{year}/User/{username}/{day.SerieADay}");
                                                foreach (var player in dayTeam.Players)
                                                {
                                                    player.Position.Year = year;
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
                                                    if (year == 2)
                                                    {
                                                        if (player.Position.X == 365)
                                                        {
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
                                                            }
                                                        }
                                                    }
                                                    player.Position.Role = player.Role;
                                                }
                                                var bitmap = new Bitmap(2500, 2500);
                                                foreach (var player in dayTeam.Players.OrderBy(x => x.Position.Status))
                                                {
                                                    Console.WriteLine($"{player.Name} with role: {player.Role}, pos:[{player.Position.X},{player.Position.Y}] has pos: {player.Position.Status}");
                                                    for (var i = player.Position.X; i < player.Position.X + 60; i++)
                                                    {
                                                        for (var j = player.Position.Y; j < player.Position.Y + 60; j++)
                                                        {
                                                            bitmap.SetPixel(j, i, GetColor());
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
                                                        x.Position.Year = year;
                                                        x.Position.IsInHome = isInHome;
                                                        if (year == 1 && x.Name.ToLower() == "pereyra" && day.SerieADay < 25)
                                                        {
                                                            x.roleX = FantacalcioV4.Role.Defensor;
                                                        }
                                                        if (year == 1 && x.Name.ToLower() == "pereirinha" && day.SerieADay >= 25)
                                                        {
                                                            x.roleX = FantacalcioV4.Role.Midfielder;
                                                        }
                                                        if (year == 1 && x.Name.ToLower() == "isla" && day.SerieADay < 25)
                                                        {
                                                            x.roleX = FantacalcioV4.Role.Midfielder;
                                                        }
                                                        if (year == 1 && x.Name.ToLower() == "camporese")
                                                        {
                                                            x.roleX = FantacalcioV4.Role.Defensor;
                                                        }
                                                        if (year == 2 && x.Name.ToLower() == "canini")
                                                        {
                                                            x.roleX = FantacalcioV4.Role.Defensor;
                                                        }
                                                        if (year == 2 && x.Name.ToLower() == "aladje gomes")
                                                        {
                                                            x.roleX = FantacalcioV4.Role.Midfielder;
                                                        }
                                                        if (year == 2 && x.Name.ToLower() == "ariaudo")
                                                        {
                                                            x.roleX = FantacalcioV4.Role.Midfielder;
                                                        }
                                                        if (year == 2 && x.Name.ToLower() == "ljajic")
                                                        {
                                                            x.roleX = FantacalcioV4.Role.Midfielder;
                                                        }
                                                        if (year == 2 && x.Name.ToLower() == "basta")
                                                        {
                                                            x.roleX = FantacalcioV4.Role.Defensor;
                                                        }
                                                        if (year == 2 && x.Name.ToLower() == "bellomo")
                                                        {
                                                            x.roleX = FantacalcioV4.Role.Midfielder;
                                                        }
                                                        if (year == 2 && day.SerieADay >= 16 && x.Name.ToLower() == "mertens")
                                                        {
                                                            x.roleX = FantacalcioV4.Role.Forward;
                                                        }
                                                        if (year == 2)
                                                        {
                                                            if (x.Position.X == 365)
                                                            {
                                                                switch (x.Position.Y)
                                                                {
                                                                    case 70:
                                                                        x.roleX = FantacalcioV4.Role.GoalKeeper;
                                                                        break;
                                                                    case 130:
                                                                        x.roleX = FantacalcioV4.Role.Defensor;
                                                                        break;
                                                                    case 190:
                                                                        x.roleX = FantacalcioV4.Role.Defensor;
                                                                        break;
                                                                    case 250:
                                                                        x.roleX = FantacalcioV4.Role.Midfielder;
                                                                        break;
                                                                    case 310:
                                                                        x.roleX = FantacalcioV4.Role.Midfielder;
                                                                        break;
                                                                    case 370:
                                                                        x.roleX = FantacalcioV4.Role.Forward;
                                                                        break;
                                                                    case 410:
                                                                        x.roleX = FantacalcioV4.Role.Forward;
                                                                        break;
                                                                    case 430:
                                                                        x.roleX = FantacalcioV4.Role.Forward;
                                                                        break;
                                                                }
                                                            }
                                                        }
                                                        x.Position.Role = x.Role;
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
                                                if (validation.IsOk)
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
                { "lorenzo", "lorenzo.catarcia@gmail.com|||Premier League" }
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
    }
}


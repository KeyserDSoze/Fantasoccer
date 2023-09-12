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
            var mappedUsers = new Dictionary<string, string>();
            mappedUsers.Add("alessandrof", "al.forbicioni@gmail.com|||Serie A");
            mappedUsers.Add("Keysersoze", "alessandro.rapiti44@gmail.com|||Serie A");
            mappedUsers.Add("Oliver", "alessiooliverio@gmail.com|||Serie A");
            mappedUsers.Add("luca", "baghiniluca@gmail.com|||Serie A");
            mappedUsers.Add("DarkLord", "christian.lucaccioni@outlook.it|||Serie A");
            mappedUsers.Add("Henrich", "crostafabio87@gmail.com|||Premier League");
            mappedUsers.Add("Squall", "daniele.fetoni@hotmail.it|||Serie A");
            mappedUsers.Add("jeffturner", "federicomartelloni@gmail.com|||Premier League");
            mappedUsers.Add("ElettroAle", "fiani.alessandro@gmail.com|||Premier League");
            mappedUsers.Add("Champion", "filippo.bossi.01@gmail.com|||Premier League");
            mappedUsers.Add("Goldenhot", "goldenhot@hotmail.it|||Serie A");
            mappedUsers.Add("saretta110", "joria87@hotmail.it|||Premier League");
            mappedUsers.Add("SirJem", "luca.morleschi@outlook.it|||Premier League");
            mappedUsers.Add("Maurizio", "m.forbicioni@hotmail.it|||Serie A");
            mappedUsers.Add("perugini", "peruginialessandro@hotmail.it|||Premier League");
            mappedUsers.Add("scarsenal", "theos.lumia@hotmail.it|||Serie A");
            mappedUsers.Add("lorenzo", "lorenzo.catarcia@gmail.com|||Premier League");
            var manager = serviceProvider.GetRequiredService<Manager>();
            //await CreateGroupAndMapUsersAsync(serviceProvider, mappedUsers, manager);
            await ParseSerieAVotesAndCalendar(serviceProvider, manager);
        }
        private static async Task CreateGroupAndMapUsersAsync(IServiceProvider serviceProvider, Dictionary<string, string> mappedUsers, Manager manager)
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
                                    string? owner = null;
                                    if (mappedUsers.ContainsKey(team.Username))
                                    {
                                        var informationForUser = mappedUsers[team.Username].Split("|||");
                                        owner = informationForUser[0];
                                    }
                                    actualBasket.Years.Last().Teams.Add(new AnnualTeam
                                    {
                                        Name = team.Name,
                                        Owner = owner,
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
    }
}


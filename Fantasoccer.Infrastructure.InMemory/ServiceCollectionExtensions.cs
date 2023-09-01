using System.Population.Random;
using System.Security.Cryptography;
using Fantasoccer.Domain.SerieA;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;
using RepositoryFramework.InMemory;

namespace Fantasoccer.InMemory
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInMemoryInfrastructures(this IServiceCollection services)
        {
            services.AddInMemoryInfrastructure<RealCalendar, int>();
            services.AddInMemoryInfrastructure<RealPlayersWrapper, int>();
            services.AddInMemoryInfrastructure<ChancedRealPlayerWrapper, YearDayKey>();
            services.AddRepository<VotedRealPlayerWrapper, YearDayKey>(builder =>
            {
                builder.WithInMemory(name: "live");
                builder.WithInMemory(name: "official");
                builder.SetOnlyQueryExposable();
            });
            services.AddInMemoryInfrastructure<Group, string>();
            services.AddInMemoryInfrastructure<Rank, LeagueKey>();
            services.AddInMemoryInfrastructure<Calendar, LeagueKey>();
            services.AddInMemoryInfrastructure<PlayedGame, GameKey>();
            services.AddInMemoryInfrastructure<SerieATeamsWrapper, int>();
            services.AddInMemoryInfrastructure<Team, TeamKey>();
            services.AddInMemoryInfrastructure<Team, TeamDayKey>(onlyQuery: false, factoryName: "day");
            services.AddInMemoryInfrastructure<AppIdentity, string>();
            services.AddInMemoryInfrastructure<FantasoccerLog, string>(true);
            services
                .AddContentRepository()
                .WithInMemoryIntegration("images");
            return services;
        }
        private static IServiceCollection AddInMemoryInfrastructure<T, TKey>(this IServiceCollection services, bool notExposable = false, bool onlyQuery = true, string? factoryName = null)
            where TKey : notnull
        {
            return services.AddRepository<T, TKey>(builder =>
            {
                builder.WithInMemory(name: factoryName);
                if (!notExposable)
                    if (onlyQuery)
                        builder.SetOnlyQueryExposable();
                    else
                        builder.SetExposable();
                else
                    builder.SetNotExposable();
            });
        }
        public static IServiceCollection AddDefaultValues(this IServiceCollection services)
        {
            services.AddWarmUp(async serviceProvider =>
            {
                var repository = serviceProvider.GetRequiredService<IRepository<AppIdentity, string>>();
                await repository.InsertAsync("alessandro.rapiti44@gmail.com", new AppIdentity
                {
                    Username = "Keyser D. Soze",
                    Email = "alessandro.rapiti44@gmail.com",
                    Groups = new List<GroupForIdentity>
                    {
                        new GroupForIdentity
                        {
                            Id = "Fantasoccer1",
                            Name = "Fantasoccer1",
                            Role = IdentityRole.Admin | IdentityRole.Participant,
                            Leagues = new List<LeagueForIdentity>
                            {
                                new LeagueForIdentity
                                {
                                    Id = "Liga1",
                                    Name = "Liga 1",
                                    Role = IdentityRole.Participant
                                },
                                new LeagueForIdentity
                                {
                                    Id = "Liga2",
                                    Name = "Liga 2",
                                    Role = IdentityRole.Reader
                                },
                                new LeagueForIdentity
                                {
                                    Id = "Liga4",
                                    Name = "Liga 4",
                                    Role = IdentityRole.Admin
                                }
                            }
                        }
                    }
                });
                var population = serviceProvider.GetRequiredService<IPopulation<Player>>();
                var teamRepository = serviceProvider.GetRequiredService<IRepository<Team, TeamKey>>();
                var teamKey = new TeamKey { Email = "alessandro.rapiti44@gmail.com", Year = FantacalcioTime.ActualYear, Group = "Fantasoccer1" };
                var team = new Team
                {
                    Name = "Soze Heaven",
                    Owner = "alessandro.rapiti44@gmail.com",
                    RevenueMoney = 230,
                    MoneyFromRank = 345,
                    Players = new List<Player>()
                };
                team.Players.AddRange(Create(1, Role.GoalKeeper, FantaSoccerRole.GoalKeeper));
                team.Players.AddRange(Create(3, Role.Defensor, FantaSoccerRole.Defensor));
                team.Players.AddRange(Create(4, Role.Midfielder, FantaSoccerRole.Midfielder));
                team.Players.AddRange(Create(3, Role.Forward, FantaSoccerRole.Forward));
                team.Players.AddRange(Create(1, Role.GoalKeeper, FantaSoccerRole.BackupGoalKeeper));
                team.Players.AddRange(Create(1, Role.Defensor, FantaSoccerRole.FirstBackupDefensor));
                team.Players.AddRange(Create(1, Role.Defensor, FantaSoccerRole.SecondBackupDefensor));
                team.Players.AddRange(Create(1, Role.Midfielder, FantaSoccerRole.FirstBackupMidfielder));
                team.Players.AddRange(Create(1, Role.Midfielder, FantaSoccerRole.SecondBackupMidfielder));
                team.Players.AddRange(Create(1, Role.Forward, FantaSoccerRole.FirstBackupForward));
                team.Players.AddRange(Create(1, Role.Forward, FantaSoccerRole.SecondBackupForward));
                team.Players.AddRange(Create(7, Role.Undefined, FantaSoccerRole.Tribune));
                List<Player> Create(int number, Role role, FantaSoccerRole fantaSoccerRole)
                {
                    var players = new List<Player>();
                    var createds = population.Populate(number);
                    foreach (var created in createds)
                    {
                        if (role != Role.Undefined)
                        {
                            created.Role = role;
                        }
                        created.Position = fantaSoccerRole;
                        created.IsActive = true;
                        created.Status = PlayerInTeamStatus.Active;
                        players.Add(created);
                    }
                    return players;
                }
                await teamRepository.InsertAsync(teamKey, team);
                var rank = serviceProvider.GetRequiredService<IRepository<Rank, LeagueKey>>();
                await rank.InsertAsync(new LeagueKey { Group = "Fantasoccer1", League = "Liga1", Year = FantacalcioTime.ActualYear }, GetRandomRank());
                await rank.InsertAsync(new LeagueKey { Group = "Fantasoccer1", League = "Liga2", Year = FantacalcioTime.ActualYear }, GetRandomRank());
                await rank.InsertAsync(new LeagueKey { Group = "Fantasoccer1", League = "Liga4", Year = FantacalcioTime.ActualYear }, GetRandomRank());
                Rank GetRandomRank()
                {
                    var newRank = new Rank
                    {
                        RankedTeams = new Dictionary<string, List<RankedTeam>>()
                    };
                    for (var i = 0; i < 4; i++)
                    {
                        var key = ((char)(65 + i)).ToString();
                        newRank.RankedTeams.Add(key, new());
                        for (var j = 0; j < 10; j++)
                        {
                            newRank.RankedTeams[key].Add(new RankedTeam
                            {
                                Name = $"Team{j}",
                                Defeats = 3,
                                Draws = 3,
                                Money = 3,
                                Goal = 3,
                                PlusMoney = 20,
                                Owner = "email",
                                Point = RandomNumberGenerator.GetInt32(20, 100),
                                SufferedGoal = 20,
                                SufferedValuePoint = 23,
                                ValuePoint = 34,
                                Victories = 45
                            });
                        }
                    }
                    return newRank;
                }
            });
            services.AddWarmUp(async serviceProvider =>
            {
                var population = serviceProvider.GetRequiredService<IPopulation<AnnualTeam>>();
                var randomCalendar = serviceProvider.GetRequiredService<IRandomCalendar>();
                var calendar = serviceProvider.GetRequiredService<IRepository<Calendar, LeagueKey>>();
                await calendar.InsertAsync(new LeagueKey { Group = "Fantasoccer1", League = "Liga1", Year = FantacalcioTime.ActualYear }, GetRandomCalendar(LeagueType.League));
                await calendar.InsertAsync(new LeagueKey { Group = "Fantasoccer1", League = "Liga2", Year = FantacalcioTime.ActualYear }, GetRandomCalendar(LeagueType.League));
                await calendar.InsertAsync(new LeagueKey { Group = "Fantasoccer1", League = "Liga4", Year = FantacalcioTime.ActualYear }, GetRandomCalendar(LeagueType.Cup));
                Calendar GetRandomCalendar(LeagueType league)
                {
                    var annualTeam = new AnnualTeam
                    {
                        Name = "Soze Heaven",
                        Owner = "alessandro.rapiti44@gmail.com"
                    };
                    var list = population.Populate(league == LeagueType.Cup ? 15 : 9).ToList();
                    list.Add(annualTeam);
                    var finalCalendar = randomCalendar.GetRandomCalendar(2, new AnnualLeague
                    {
                        Id = Guid.NewGuid().ToString(),
                        Settings = new LeagueSetting
                        {
                            DelayedDay = 2
                        },
                        Type = league,
                        Teams = list
                    });
                    return finalCalendar;
                }
            });
            return services;
        }
    }
}

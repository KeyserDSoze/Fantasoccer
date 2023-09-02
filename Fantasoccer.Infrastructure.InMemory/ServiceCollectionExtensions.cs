using System.Linq;
using System.Population.Random;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Security.Principal;
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
        private sealed class ParameterBuilder<T1, T2, T3, T4>
        {
            public T1[] First { get; set; }
            public T2[] Second { get; set; }
            public T3[] Third { get; set; }
            public T4[] Fourth { get; set; }
            public ParameterBuilder<T1, T2, T3, T4> SetFirst(params T1[] t1s) { First = t1s; return this; }
            public ParameterBuilder<T1, T2, T3, T4> SetSecond(params T2[] t2s) { Second = t2s; return this; }
            public ParameterBuilder<T1, T2, T3, T4> SetThird(params T3[] t3s) { Third = t3s; return this; }
            public ParameterBuilder<T1, T2, T3, T4> SetFourth(params T4[] t4s) { Fourth = t4s; return this; }
        }
        private sealed class LeagueForCreateRank
        {
            public LeagueKey LeagueKey { get; set; }
            public List<AnnualTeam> Teams { get; set; }
            public LeagueType Type { get; set; }
        }
        public static IServiceCollection AddDefaultValues(this IServiceCollection services)
        {
            services.AddWarmUp(async serviceProvider =>
            {
                DateTime start = DateTime.UtcNow;
                var users = new List<string> { "alessandro.rapiti44@gmail.com", "daniele.fetoni@vetrya.com",
                "daniele.marpioni@vetrya.com","daniele.catoni@vetrya.com","daniele.tufoni@vetrya.com",
                "daniele.maloni@vetrya.com","daniele.astoni@vetrya.com","daniele.totoni@vetrya.com",
                "daniele.marcioni@vetrya.com","daniele.castoni@vetrya.com","daniele.tufroni@vetrya.com",
                "daniele.marloni@vetrya.com","daniele.astonti@vetrya.com","daniele.tottoni@vetrya.com",
                "daniele.marcionia@vetrya.com","daniele.castonia@vetrya.com","daniele.tufronia@vetrya.com",
                "daniele.marlonia@vetrya.com","daniele.astontia@vetrya.com","daniele.tottonia@vetrya.com",
                "daniele.marlonire@vetrya.com","daniele.astontire@vetrya.com","daniele.tottonire@vetrya.com",
                "daniele.marcioniare@vetrya.com","daniele.castoniare@vetrya.com","daniele.tufroniare@vetrya.com",
                "daniele.marloniare@vetrya.com","daniele.astontiare@vetrya.com","daniele.tottoniare@vetrya.com" };
                var realTeams = new List<string> { "Atalanta", "Bologna", "Cagliari", "Empoli", "Fiorentina", "Frosinone", "Genoa", "Inter", "Juventus", "Lazio", "Lecce", "Milan", "Monza", "Napoli", "Roma", "Salernitana", "Sassuolo", "Torino", "Udinese", "Verona" };
                var repository = serviceProvider.GetRequiredService<IRepository<AppIdentity, string>>();
                var parameterBuilderForUsers = new ParameterBuilder<int, int, int, int>();
                for (var i = 0; i < 20; i++)
                {
                    var email = users[i];
                    var leagues = new List<int>();
                    leagues.Add(i % 2 + 1);
                    if (i < 16)
                    {
                        leagues.Add(3);
                    }
                    await CreateUserAsync(email, parameterBuilderForUsers.SetFirst(1, 2).SetSecond(leagues.ToArray()), 0, 123);
                }
                async Task CreateUserAsync(string email, ParameterBuilder<int, int, int, int> builder, int admin, int reader)
                {
                    var groups = new List<GroupForIdentity>();
                    foreach (var group in builder.First)
                    {
                        var groupX = new GroupForIdentity
                        {
                            Id = $"Fantasoccer{group}",
                            Name = $"Fantasoccer{group}",
                            Role = IdentityRole.Admin | IdentityRole.Participant,
                            Leagues = new List<LeagueForIdentity>()
                        };
                        groups.Add(groupX);
                        foreach (var league in builder.Second)
                        {
                            groupX.Leagues.Add(new LeagueForIdentity
                            {
                                Id = $"Liga{league}",
                                Name = $"Liga {league}",
                                Type = league == 3 ? LeagueType.Cup : LeagueType.League,
                                Role = league == admin ? IdentityRole.Admin : (league == reader ? IdentityRole.Reader : IdentityRole.Participant)
                            });
                        }
                    }
                    await repository!.InsertAsync(email, new AppIdentity
                    {
                        Username = email == "alessandro.rapiti44@gmail.com" ? "Keyser D. Soze" : email.Split('@')[0],
                        Email = email,
                        Groups = groups
                    });
                    Console.WriteLine($"User with {email} created in {DateTime.UtcNow.Subtract(start).TotalMilliseconds}.");
                    start = DateTime.UtcNow;
                }
                var createdUsers = await repository.ToListAsEntityAsync();
                var population = serviceProvider.GetRequiredService<IPopulation<Player>>();
                population
                    .Setup()
                    .WithPattern(x => x.Name, "[A-Za-z]{5,10}")
                    .WithPattern(x => x.RealTeam.Name, string.Join('|', realTeams));
                var teamRepository = serviceProvider.GetRequiredService<IRepository<Team, TeamKey>>();
                foreach (var createdUser in createdUsers)
                {
                    await CreateTeamAsync(createdUser!);
                }
                async Task CreateTeamAsync(AppIdentity identity)
                {
                    foreach (var group in identity.Groups)
                    {
                        var teamKey = new TeamKey { Email = identity.Email, Year = FantacalcioTime.ActualYear, Group = group.Id };
                        var team = new Team
                        {
                            Name = identity.Email == "alessandro.rapiti44@gmail.com" ? "Soze Heaven" : $"{identity.Username} Team",
                            Owner = identity.Email,
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
                                created.Price = RandomNumberGenerator.GetInt32(1, 20);
                                players.Add(created);
                            }
                            return players;
                        }
                        await teamRepository.InsertAsync(teamKey, team);
                        Console.WriteLine($"Team in group {teamKey.Group} for {teamKey.Email} created in {DateTime.UtcNow.Subtract(start).TotalMilliseconds}.");
                        start = DateTime.UtcNow;
                    }
                }
                var rank = serviceProvider.GetRequiredService<IRepository<Rank, LeagueKey>>();
                var ranksForCreation = new List<LeagueForCreateRank>();
                foreach (var createdUser in createdUsers)
                {
                    foreach (var groupForCreatedUser in createdUser.Groups)
                    {
                        foreach (var leagueForCreatedUser in groupForCreatedUser.Leagues)
                        {
                            var leagueKey = new LeagueKey { Group = groupForCreatedUser.Id, League = leagueForCreatedUser.Id, Year = FantacalcioTime.ActualYear };
                            var league = ranksForCreation.FirstOrDefault(x => x.LeagueKey.Group == leagueKey.Group && x.LeagueKey.League == leagueKey.League);
                            if (league == null)
                            {
                                league = new LeagueForCreateRank
                                {
                                    LeagueKey = leagueKey,
                                    Teams = new(),
                                    Type = leagueForCreatedUser.Type
                                };
                                ranksForCreation.Add(league);
                            }
                            league.Teams.Add(new AnnualTeam
                            {
                                Name = createdUser.Email == "alessandro.rapiti44@gmail.com" ? "Soze Heaven" : $"{createdUser.Username} Team",
                                Owner = createdUser.Email
                            });
                        }
                    }
                }
                var randomCalendar = serviceProvider.GetRequiredService<IRandomCalendar>();
                var calendar = serviceProvider.GetRequiredService<IRepository<Calendar, LeagueKey>>();
                foreach (var rankForCreation in ranksForCreation)
                {
                    await calendar.InsertAsync(rankForCreation.LeagueKey, GetRandomCalendar(rankForCreation.Type, rankForCreation.Teams));
                    Console.WriteLine($"Calendar created for group {rankForCreation.LeagueKey.Group} and league {rankForCreation.LeagueKey.League} for year {rankForCreation.LeagueKey.Year} in {DateTime.UtcNow.Subtract(start).TotalMilliseconds}.");
                    start = DateTime.UtcNow;

                    await rank.InsertAsync(
                        rankForCreation.LeagueKey, GetRandomRank(rankForCreation.Teams, rankForCreation.Type));
                    Console.WriteLine($"Rank created for group {rankForCreation.LeagueKey.Group} and league {rankForCreation.LeagueKey.League} for year {rankForCreation.LeagueKey.Year} in {DateTime.UtcNow.Subtract(start).TotalMilliseconds}.");
                    start = DateTime.UtcNow;
                }
                Calendar GetRandomCalendar(LeagueType league, List<AnnualTeam> annualTeams)
                {
                    var finalCalendar = randomCalendar.GetRandomCalendar(2, new AnnualLeague
                    {
                        Id = Guid.NewGuid().ToString(),
                        Settings = new LeagueSetting
                        {
                            DelayedDay = 2
                        },
                        Type = league,
                        Teams = annualTeams
                    });
                    return finalCalendar;
                }
                Rank GetRandomRank(List<AnnualTeam> teams, LeagueType type)
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
            return services;
        }
    }
}

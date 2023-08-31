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
            services.AddInMemoryInfrastructure<Team, TeamDayKey>();
            services.AddInMemoryInfrastructure<AppIdentity, string>();
            services.AddInMemoryInfrastructure<FantasoccerLog, string>(true);
            services
                .AddContentRepository()
                .WithInMemoryIntegration("images");
            return services;
        }
        private static IServiceCollection AddInMemoryInfrastructure<T, TKey>(this IServiceCollection services, bool notExposable = false)
            where TKey : notnull
        {
            return services.AddRepository<T, TKey>(builder =>
            {
                builder.WithInMemory();
                if (!notExposable)
                    builder.SetOnlyQueryExposable();
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
                        var key = ((char)(41 + i)).ToString();
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

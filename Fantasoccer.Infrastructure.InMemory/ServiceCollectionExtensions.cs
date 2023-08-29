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
                            Id = Guid.NewGuid().ToString(),
                            Name = "Fantasoccer1",
                            Role = IdentityRole.Admin | IdentityRole.Participant,
                            Leagues = new List<LeagueForIdentity>
                            {
                                new LeagueForIdentity
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    Name = "Liga 1",
                                    Role = IdentityRole.Participant
                                },
                                new LeagueForIdentity
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    Name = "Liga 2",
                                    Role = IdentityRole.Reader
                                },
                                new LeagueForIdentity
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    Name = "Liga 4",
                                    Role = IdentityRole.Admin
                                }
                            }
                        }
                    }
                });
            });
            return services;
        }
    }
}

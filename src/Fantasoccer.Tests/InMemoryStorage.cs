using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fantasoccer.Domain.SerieA;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework.InMemory;

namespace Fantasoccer.Tests
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInMemoryInfrastructures(this IServiceCollection services)
        {
            services.AddInMemoryInfrastructure<RealCalendar, int>();
            services.AddInMemoryInfrastructure<RealPlayersWrapper, int>();
            services.AddInMemoryInfrastructure<ChancedRealPlayer, YearDayKey>();
            services.AddInMemoryInfrastructure<Group, string>();
            services.AddInMemoryInfrastructure<Rank, LeagueKey>();
            services.AddInMemoryInfrastructure<Calendar, LeagueKey>();
            services.AddInMemoryInfrastructure<PlayedGame, GameKey>();
            services.AddInMemoryInfrastructure<SerieATeamsWrapper, int>();
            services.AddInMemoryInfrastructure<FantacalcioLog, string>();
            return services;
        }
        private static IServiceCollection AddInMemoryInfrastructure<T, TKey>(this IServiceCollection services)
            where TKey : notnull
        {
            return services.AddRepository<T, TKey>(builder => builder.WithInMemory());
        }
    }
}

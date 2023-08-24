using FantacalcioV4.Database;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Rystem;
using Rystem.Background;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Fantasoccer.Business
{
    public static class ManagerExtensions
    {
        public static IServiceCollection AddFantacalcioApi(this IServiceCollection services, IdentityOptions options)
        {
            services.AddFantacalcioDatabase();
            services.AddSingleton<Logger>();
            services.AddHttpContextAccessor();
            services.AddScoped<FantacalcioState>();
            services.AddScoped<GroupApi>();
            services.AddScoped<CoreApi>();
            services.AddScoped<JobApi>();
            services.AddScoped<LeagueApi>();
            services.AddScoped<LiveApi>();
            services.AddScoped<IdentityApi>();
            services.AddScoped<MarketApi>();
            services.AddScoped<Manager>();
            services.AddSingleton(options);
            services.AddScoped<IdentityMiddleware>();
            services.AddLog<Team>();
            services.UseDocumentOn<Debug>()
                .WithAzure()
                .WithTableStorage(new Rystem.Azure.Integration.Storage.TableStorageConfiguration("Log"))
                .WithPrimaryKey(x => x.Name)
                    .WithSecondaryKey(x => x.RowKey)
                    .WithTimestamp(x => x.Timestamp)
                    .Configure();
            Queue.Create(new SequenceProperty<ChangedTeam>(string.Empty,
                200,
                TimeSpan.FromMinutes(2),
                ChangedTeamQueueManager.ExecuteAsync),
                    QueueType.FirstInFirstOut);
            PlayerValue.SetPlayerValue((key) =>
            {
                string[] keys = key.Split('/');
                int year = int.Parse(keys[0]);
                var player = new AllPlayerKey { Year = year }.Instance().FirstOrDefault(x => x.Key == key);
                if (player == default)
                    return default;
                else
                    return player with
                    {
                        RealTeam = AllTeamKey.All.Instance()
                        .FirstOrDefault(x => x.Abbreviation.ToLower() == player.RealTeam.ToLower())?
                        .PrimaryKey ?? string.Empty
                    };
            });
            TeamValue.SetTeamName((id) =>
            {
                var miniTeam = new MiniTeam(id);
                var user = new IdentityKey { Username = miniTeam.Username }.Instance();
                var team = user.Groups.FirstOrDefault(x => x.Name == miniTeam.GroupName)?
                    .Teams?
                    .FirstOrDefault(x => x.Year == miniTeam.Year);
                return team?.Name ?? string.Empty;
            });
            TeamValue.SetTeam((id, serieADay) => new TeamContext(id, serieADay).GetAsync().ToResult());
            return services;
        }
        public static IApplicationBuilder UseFantacalcioAuthentication(this IApplicationBuilder app)
        {
            app.UseMiddleware<IdentityMiddleware>();
            return app;
        }
    }
}

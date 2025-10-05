using System.Security.Cryptography;
using Fantasoccer.Domain;
using Fantasoccer.Infrastructure.Storage.Logger;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;

namespace Fantasoccer.Infrastructure
{
    public static class ServiceCollectionExtensions
    {
        public static async Task<SecurityOptions> AddSecurityOptionsAsync(this IServiceCollection services,
            string connectionString)
        {
            var newServiceCollection = new ServiceCollection();
            newServiceCollection.AddRepository<SecurityOptions, string>(settings =>
            {
                settings.WithTableStorage(
                    storageBuilder =>
                    {
                        storageBuilder.Settings.ConnectionString = connectionString;
                        storageBuilder
                            .WithPartitionKey(x => x.Name, x => x);
                    });
            });

            var securityOptions = await newServiceCollection.ExecuteUntilNowAsync(
                async (IRepository<SecurityOptions, string> rsaRepository) =>
                {
                    if (!await rsaRepository!.ExistAsync("token"))
                    {
                        using var rsaNew = new RSACryptoServiceProvider(2048);
                        var rsaOptions = rsaNew.ExportParameters(true).ToRsaOptions();
                        await rsaRepository.InsertAsync("token", new("token", rsaOptions));
                    }
                    var options = await rsaRepository.GetAsync("token");
                    return options;
                });

            services.AddSingleton(securityOptions!);
            return securityOptions;
        }
        public static async Task<IServiceCollection> AddStorageInfrastructuresAsync(this IServiceCollection services, string storageConnectionString)
        {
            await services.AddBlobStorage<RealCalendar, int>(storageConnectionString);
            await services.AddBlobStorage<RealRank, int>(storageConnectionString);
            await services.AddBlobStorage<RealPlayersWrapper, int>(storageConnectionString);
            await services.AddBlobStorage<RealPlayersWrapper, LeagueKey>(storageConnectionString, "random", "randomizedplayers");
            await services.AddBlobStorage<ChancedRealPlayerWrapper, YearDayKey>(storageConnectionString);
            await services.AddRepositoryAsync<VotedRealPlayerWrapper, YearDayKey>(async builder =>
            {
                await builder.WithBlobStorageAsync(storageBuilder =>
                {
                    storageBuilder.Settings.ConnectionString = storageConnectionString;
                    storageBuilder.Settings.ContainerName = "live";
                }, "live");
                await builder.WithBlobStorageAsync(storageBuilder =>
                {
                    storageBuilder.Settings.ConnectionString = storageConnectionString;
                    storageBuilder.Settings.ContainerName = "notification";
                }, "notification");
                await builder.WithBlobStorageAsync(storageBuilder =>
                {
                    storageBuilder.Settings.ConnectionString = storageConnectionString;
                    storageBuilder.Settings.ContainerName = "official";
                }, "official");
            });
            await services.AddBlobStorage<PushNotificationSent, YearDayKey>(storageConnectionString, null, "pushnotificationsent");
            await services.AddBlobStorage<Group, string>(storageConnectionString);
            await services.AddBlobStorage<Settings, string>(storageConnectionString);
            await services.AddBlobStorage<HallOfFame, GeneralLeagueKey>(storageConnectionString);
            await services.AddBlobStorage<LiveGroup, string>(storageConnectionString);
            await services.AddBlobStorage<Rank, LeagueKey>(storageConnectionString);
            await services.AddBlobStorage<MarketWrapper, LeagueKey>(storageConnectionString);
            await services.AddBlobStorage<Rank, YearDayLeague>(storageConnectionString, "day", "dailyrank");
            await services.AddBlobStorage<Calendar, LeagueKey>(storageConnectionString);
            await services.AddBlobStorage<RealTeamWrapper, int>(storageConnectionString);
            await services.AddBlobStorage<StatPlayersWrapper, int>(storageConnectionString);
            await services.AddBlobStorage<PlayedGame, GameKey>(storageConnectionString);
            await services.AddBlobStorage<Team, TeamKey>(storageConnectionString);
            await services.AddBlobStorage<Team, TeamDayKey>(storageConnectionString, "day", "dailyteams");
            await services.AddBlobStorage<TeamWithAi, TeamDayKey>(storageConnectionString);
            await services.AddBlobStorage<AppIdentity, string>(storageConnectionString);
            await services.AddBlobStorage<NewsWrapper, YearDayGroup>(storageConnectionString, "news", "news");
            await services.AddBlobStorage<PushNotificationSettings, string>(storageConnectionString, null, "pushnotification");
            await services.AddBlobStorage<AuctionWrapper, string>(storageConnectionString, null, "auction");
            await services.AddRepositoryAsync<FantasoccerLog, string>(async builder =>
            {
                await builder.WithTableStorageAsync(storageBuilder =>
                {
                    storageBuilder.Settings.ConnectionString = storageConnectionString;
                    storageBuilder.Settings.TableName = "logs";
                    storageBuilder
                        .WithPartitionKey(x => x.PrimaryKey, x => x)
                        .WithRowKey(x => x.SecondaryKey)
                        .WithTimestamp(x => x.Timestamp)
                        .WithTableStorageKeyReader<LoggerKeyReader>();
                });
            });
            await services
                .AddContentRepository()
                .WithBlobStorageIntegrationAsync(builder =>
                {
                    builder.ContainerName = "images";
                    builder.ConnectionString = storageConnectionString;
                    builder.IsPublic = true;
                }, "images");
            return services;
        }
        private static Task<IServiceCollection> AddBlobStorage<T, TKey>(this IServiceCollection services,
            string connectionString,
            string? factoryName = null,
            string? tableName = null)
            where TKey : notnull
        {
            return services.AddRepositoryAsync<T, TKey>(async builder =>
             {
                 await builder.WithBlobStorageAsync(storageBuilder =>
                 {
                     storageBuilder.Settings.ConnectionString = connectionString;
                     if (tableName != null)
                         storageBuilder.Settings.ContainerName = tableName;
                 }, factoryName);
             });
        }
    }
}

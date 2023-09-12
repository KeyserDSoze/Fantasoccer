using Microsoft.Extensions.DependencyInjection;
using Rystem;
using Rystem.Business;
using System;
using System.Collections.Generic;

namespace FantacalcioV4.Database
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddFantacalcioDatabase(this IServiceCollection services)
        {
            services
                .UseCacheWithKey<AllVoteKey, List<SummedVotesPlayer>>()
                    .WithAzure()
                    .WithBlobStorage(new Rystem.Azure.Integration.Storage.BlobStorageConfiguration("fantacalciocache"))
                    .Configure()
                .UseCacheWithKey<AllPlayerKey, List<RealPlayer>>()
                    .WithMemory(new CacheConfiguration(TimeSpan.FromDays(365)))
                    .Configure()
                .UseCacheWithKey<AnnualLeagueKey, PlayedAnnualLeague>()
                    .WithMemory(new CacheConfiguration(TimeSpan.FromDays(365)))
                    .Configure()
                .UseCacheWithKey<CalendarKey, Calendar>()
                    .WithAzure()
                    .WithBlobStorage(new Rystem.Azure.Integration.Storage.BlobStorageConfiguration("fantacalciocache"))
                    .Configure()
                .UseCacheWithKey<GroupKey, Group>()
                    .WithMemory(new CacheConfiguration(TimeSpan.FromDays(365)))
                    .Configure()
                .UseCacheWithKey<IdentityKey, Identity>()
                    .WithMemory(new CacheConfiguration(TimeSpan.FromDays(365)))
                    .Configure()
                .UseCacheWithKey<RankKey, Rank>()
                    .WithAzure()
                    .WithBlobStorage(new Rystem.Azure.Integration.Storage.BlobStorageConfiguration("fantacalciocache"))
                    .Configure()
                .UseCacheWithKey<RealCalendarKey, RealCalendar>()
                    .WithMemory(new CacheConfiguration(TimeSpan.FromDays(365)))
                    .Configure()
                .UseCacheWithKey<LiveKey, ActualLive>()
                    .WithMemory(new CacheConfiguration(TimeSpan.FromDays(365)))
                    .Configure()
                .UseCacheWithKey<AllTeamKey, IEnumerable<StoredSerieATeam>>()
                    .WithMemory(new CacheConfiguration(TimeSpan.FromDays(365)))
                    .Configure()
                .UseDataOn<FantacalcioContext>()
                    .WithAzure()
                    .WithBlockBlob(new("FantacalcioDB"))
                    .WithName(x => x.Name)
                    .Configure()
                .UseDataOn<ImageWrapper>()
                    .WithAzure()
                    .WithBlockBlob(new("static"))
                    .WithName(x => x.Name)
                    .Configure()
                .UseDocumentOn<StoredSerieATeam>()
                    .WithAzure()
                    .WithTableStorage(new Rystem.Azure.Integration.Storage.TableStorageConfiguration("SerieA"))
                    .WithPrimaryKey(x => x.PrimaryKey)
                    .WithSecondaryKey(x => x.SecondaryKey)
                    .WithTimestamp(x => x.Timestamp)
                    .Configure()
                .UseDocumentOn<StoredPushNotification>()
                    .WithAzure()
                    .WithTableStorage(new Rystem.Azure.Integration.Storage.TableStorageConfiguration("PushAccounts"))
                    .WithPrimaryKey(x => x.PrimaryKey)
                    .WithSecondaryKey(x => x.SecondaryKey)
                    .WithTimestamp(x => x.Timestamp)
                    .Configure()
                .UseDocumentOn<StoredIdentity>()
                    .WithAzure()
                    .WithTableStorage(new Rystem.Azure.Integration.Storage.TableStorageConfiguration("Accounts"))
                    .WithPrimaryKey(x => x.PrimaryKey)
                    .WithSecondaryKey(x => x.SecondaryKey)
                    .WithTimestamp(x => x.Timestamp)
                    .Configure()
                .UseDocumentOn<StoredGroup>()
                    .WithAzure()
                    .WithTableStorage(new Rystem.Azure.Integration.Storage.TableStorageConfiguration("Groups"))
                    .WithPrimaryKey(x => x.PrimaryKey)
                    .WithSecondaryKey(x => x.SecondaryKey)
                    .WithTimestamp(x => x.Timestamp)
                    .Configure();
            return services;
        }
        public static IServiceCollection AddLog<T>(this IServiceCollection services)
        {
            return services.UseDocumentOn<FantacalcioLog<T>>()
                .WithAzure()
                .WithTableStorage(new Rystem.Azure.Integration.Storage.TableStorageConfiguration($"{typeof(T).Name}Logging"))
                .WithPrimaryKey(x => x.PrimaryKey)
                    .WithSecondaryKey(x => x.SecondaryKey)
                    .WithTimestamp(x => x.Timestamp)
                    .Configure();
        }
    }
}

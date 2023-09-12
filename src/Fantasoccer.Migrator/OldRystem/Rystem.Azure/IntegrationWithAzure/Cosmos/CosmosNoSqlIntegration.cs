using System.Linq.Expressions;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace Rystem.Azure.Integration.Cosmos
{
    public sealed class CosmosNoSqlIntegration : IWarmUp
    {
        public CosmosConfiguration Configuration { get; }
        private Container Context;
        private readonly string RaceId = Guid.NewGuid().ToString("N");
        private readonly CosmosAccount Options;
        public CosmosNoSqlIntegration(CosmosConfiguration configuration, CosmosAccount options)
        {
            Configuration = configuration;
            Options = options;
        }
        private async Task<Container> GetContextAsync()
        {
            //if (Context == default)
            //    await RaceCondition.RunAsync(async () =>
            //    {
            //        if (Context == default)
            //        {
            //            CosmosClient cosmosClient = default;
            //            if (!string.IsNullOrWhiteSpace(Options.AccountKey) || Options.UseKeyVault)
            //            {
            //                cosmosClient = new CosmosClient(
            //                await (Options as IRystemOptions).GetConnectionStringAsync(),
            //                Configuration.ClientOptions);
            //            }
            //            else
            //            {
            //                cosmosClient = new CosmosClient(
            //                    Options.AccountName,
            //                    new DefaultAzureCredential(),
            //                    Configuration.ClientOptions);
            //            }
            //            var response = await cosmosClient.CreateDatabaseIfNotExistsAsync(Configuration.DatabaseName ?? Options.DatabaseName,
            //                 Configuration.Database?.Throughput,
            //                 Configuration.Database?.RequestOptions
            //                 );
            //            if (Configuration.Database?.Throughput != default)
            //                await response.Database.ReplaceThroughputAsync(Configuration.Database.Throughput, Configuration.Database.RequestOptions, default);
            //            var containerResponse = await response.Database.CreateContainerIfNotExistsAsync(
            //                Configuration.ContainerProperties ?? new ContainerProperties($"{Configuration.Name}IndexPolicy", "/PrimaryKey"),
            //                Configuration.Container?.Throughput,
            //                Configuration.Container?.RequestOptions);
            //            if (Configuration.Container?.Throughput != default)
            //                await containerResponse.Container.ReplaceThroughputAsync(Configuration.Container.Throughput, Configuration.Container.RequestOptions, default);
            //            Context = containerResponse.Container;
            //        }
            //    }, RaceId);
            return Context;
        }
        public async Task<bool> DeleteAsync<T>(string id, PartitionKey partitionKey, ItemRequestOptions requestOptions)
        {
            var client = Context ?? await GetContextAsync();
            var response = await client.DeleteItemAsync<T>(id, partitionKey, requestOptions, default);
            return response.StatusCode == System.Net.HttpStatusCode.OK || response.StatusCode == System.Net.HttpStatusCode.Created || response.StatusCode == System.Net.HttpStatusCode.Accepted;
        }
        public async Task<bool> DeleteBatchAsync<T>(IEnumerable<string> ids, PartitionKey partitionKey, ItemRequestOptions requestOptions)
        {
            bool allIsDone = true;
            List<Task> executions = new();
            foreach (var id in ids)
            {
                executions.Add(ExecuteAsync());

                async Task ExecuteAsync()
                    => allIsDone &= await DeleteAsync<T>(id, partitionKey, requestOptions);
            }
            await Task.WhenAll(executions);
            return allIsDone;
        }
        public async IAsyncEnumerable<T> ReadAsync<T>(Expression<Func<T, bool>> expression = default, int? takeCount = default, QueryRequestOptions requestOptions = default, bool allowSyncrhonousQueryExecution = false, CosmosLinqSerializerOptions cosmosLinqSerializerOptions = default)
        {
            var client = Context ?? await GetContextAsync();

            string continuationToken = default;
            int counter = 0;
            do
            {
                IQueryable<T> query = client.GetItemLinqQueryable<T>(allowSyncrhonousQueryExecution, continuationToken, requestOptions, cosmosLinqSerializerOptions);
                if (expression != default)
                    query = query.Where(expression);
                var response = query.ToFeedIterator();
                var results = await response.ReadNextAsync();
                foreach (var value in results)
                {
                    if (takeCount != default && counter >= takeCount)
                        break;
                    yield return value;
                    counter++;
                }
                continuationToken = response.HasMoreResults ? results.ContinuationToken : default;
            } while (continuationToken != default);
        }
        public async Task<T> ReadAsync<T>(string id, PartitionKey partitionKey, ItemRequestOptions requestOptions)
        {
            var client = Context ?? await GetContextAsync();
            var response = await client.ReadItemAsync<T>(id, partitionKey, requestOptions);
            return response.Resource;
        }
        public async Task<bool> ExistAsync<T>(string id, PartitionKey partitionKey, ItemRequestOptions requestOptions)
        {
            var client = Context ?? await GetContextAsync();
            var response = await client.ReadItemAsync<T>(id, partitionKey, requestOptions);
            return response.StatusCode != System.Net.HttpStatusCode.NotFound;
        }
        public async Task<bool> UpdateAsync<T>(PartitionKey partitionKey, T entity, ItemRequestOptions itemRequestOptions)
        {
            var client = Context ?? await GetContextAsync();
            var response = await client.UpsertItemAsync(entity, partitionKey, itemRequestOptions);
            return response.StatusCode == System.Net.HttpStatusCode.OK || response.StatusCode == System.Net.HttpStatusCode.Created || response.StatusCode == System.Net.HttpStatusCode.Accepted;
        }

        public async Task<bool> UpdateBatchAsync<T>(PartitionKey partitionKey, IEnumerable<T> entities, ItemRequestOptions itemRequestOptions)
        {
            bool allIsDone = true;
            List<Task> executions = new();
            foreach (var entity in entities)
            {
                executions.Add(ExecuteAsync());

                async Task ExecuteAsync()
                    => allIsDone &= await UpdateAsync(partitionKey, entity, itemRequestOptions);
            }
            await Task.WhenAll(executions);
            return allIsDone;
        }

        public async Task<bool> WarmUpAsync()
        {
            await GetContextAsync();
            return true;
        }
    }
}

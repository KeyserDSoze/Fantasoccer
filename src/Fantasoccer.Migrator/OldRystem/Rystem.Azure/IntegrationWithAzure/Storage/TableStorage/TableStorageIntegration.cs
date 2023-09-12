using Microsoft.Azure.Cosmos.Table;

namespace Rystem.Azure.Integration.Storage
{
    public sealed class TableStorageIntegration : BaseStorageClient, IWarmUp
    {
        private CloudTable Context;
        private readonly string RaceId = Guid.NewGuid().ToString("N");
        public TableStorageConfiguration Configuration { get; }
        public TableStorageIntegration(TableStorageConfiguration configuration, StorageAccount options) : base(options)
            => Configuration = configuration;
        private async Task<CloudTable> GetContextAsync()
        {
            //if (Context == default)
            //    await RaceCondition.RunAsync(async () =>
            //    {
            //        if (Context == default)
            //        {
            //            var storageAccount = CloudStorageAccount.Parse(await (Account as IRystemOptions).GetConnectionStringAsync());
            //            var client = Account.TableClientConfiguration != default ?
            //                    storageAccount.CreateCloudTableClient(Account.TableClientConfiguration) : storageAccount.CreateCloudTableClient();
            //            var tableClient = client.GetTableReference(Configuration.Name);
            //            if (!await tableClient.ExistsAsync())
            //                await tableClient.CreateIfNotExistsAsync();
            //            Context = tableClient;
            //        }
            //    }, RaceId);
            if (Context == default)
            {
                var storageAccount = CloudStorageAccount.Parse(await (Account as IRystemOptions).GetConnectionStringAsync());
                var client = Account.TableClientConfiguration != default ?
                        storageAccount.CreateCloudTableClient(Account.TableClientConfiguration) : storageAccount.CreateCloudTableClient();
                var tableClient = client.GetTableReference(Configuration.Name);
                if (!await tableClient.ExistsAsync())
                    await tableClient.CreateIfNotExistsAsync();
                Context = tableClient;
            }
            return Context;
        }
        private const string Asterisk = "*";
        public async Task<bool> DeleteAsync(DynamicTableEntity entity)
        {
            var client = Context ?? await GetContextAsync();
            if (string.IsNullOrWhiteSpace(entity.ETag))
                entity.ETag = Asterisk;
            TableOperation operation = TableOperation.Delete(entity);
            return (await client.ExecuteAsync(operation)).HttpStatusCode == 204;
        }
        public async Task<DynamicTableEntity> GetAsync(DynamicTableEntity entity)
        {
            var client = Context ?? await GetContextAsync();
            TableOperation operation = TableOperation.Retrieve<DynamicTableEntity>(entity.PartitionKey, entity.RowKey);
            TableResult result = await client.ExecuteAsync(operation);
            return result.Result == default ? default : (DynamicTableEntity)result.Result;
        }

        public async Task<List<DynamicTableEntity>> QueryAsync(string query = default, int? takeCount = default, TableContinuationToken token = default)
        {
            var client = Context ?? await GetContextAsync();
            List<DynamicTableEntity> items = new();
            do
            {
                TableQuerySegment<DynamicTableEntity> seg = await client.ExecuteQuerySegmentedAsync(new TableQuery<DynamicTableEntity>() { FilterString = query, TakeCount = takeCount }, token);
                token = seg.ContinuationToken;
                items.AddRange(seg);
                if (takeCount != default && items.Count >= takeCount)
                    break;
            } while (token != default);
            return items;
        }
        public async Task<bool> UpdateAsync(DynamicTableEntity entity)
        {
            var client = Context ?? await GetContextAsync();
            TableOperation operation = TableOperation.InsertOrReplace(entity);
            return (await client.ExecuteAsync(operation)).HttpStatusCode == 204;
        }
        public async Task<bool> UpdateBatchAsync(IEnumerable<DynamicTableEntity> entities)
        {
            var client = Context ?? await GetContextAsync();
            bool result = true;
            foreach (var groupedEntity in entities.GroupBy(x => x.PartitionKey))
            {
                TableBatchOperation batch = new();
                foreach (DynamicTableEntity entity in groupedEntity)
                {
                    batch.InsertOrReplace(entity);
                    if (batch.Count == 100)
                    {
                        IList<TableResult> results = await client.ExecuteBatchAsync(batch);
                        result &= results.All(x => x.HttpStatusCode == 204);
                        batch = new TableBatchOperation();
                    }
                }
                if (batch.Count > 0)
                    result &= (await client.ExecuteBatchAsync(batch)).All(x => x.HttpStatusCode == 204);
            }
            return result;
        }
        public async Task<bool> DeleteBatchAsync(IEnumerable<DynamicTableEntity> entities)
        {
            var client = Context ?? await GetContextAsync();
            bool result = true;
            foreach (var groupedEntity in entities.GroupBy(x => x.PartitionKey))
            {
                TableBatchOperation batch = new();
                foreach (DynamicTableEntity entity in groupedEntity)
                {
                    batch.Delete(entity);
                    if (batch.Count == 100)
                    {
                        IList<TableResult> results = await client.ExecuteBatchAsync(batch);
                        result &= (results.All(x => x.HttpStatusCode == 204));
                        batch = new TableBatchOperation();
                    }
                }
                if (batch.Count > 0)
                    result &= (await client.ExecuteBatchAsync(batch)).All(x => x.HttpStatusCode == 204);
            }
            return result;
        }

        public async Task<bool> WarmUpAsync()
        {
            await GetContextAsync();
            return true;
        }
    }
}

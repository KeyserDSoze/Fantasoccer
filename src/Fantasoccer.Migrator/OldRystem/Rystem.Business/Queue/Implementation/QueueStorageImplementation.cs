using Rystem.Azure.Integration.Storage;
using Rystem.Background;
using Rystem.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Rystem.Business.Queue.Implementation
{
    internal sealed class QueueStorageImplementation<TEntity> : IQueueImplementation<TEntity>
    {
        private readonly QueueStorageIntegration Integration;
        internal QueueStorageImplementation(QueueStorageIntegration integration)
        {
            Integration = integration;
        }
        public Task<bool> CleanAsync()
            => throw new NotImplementedException("Queue storage doesn't allow this operation.");
        public Task<bool> DeleteScheduledAsync(long messageId)
            => throw new NotImplementedException("Queue storage doesn't allow this operation.");
        public string GetName()
           => this.Integration.Configuration.Name;
        private readonly string ListenerId = $"{nameof(QueueStorageImplementation<TEntity>)}{Guid.NewGuid():N}";
        private bool IsListening = false;
        public Task ListenAsync(Func<TEntity, string, object, Task> callback, Func<Exception, Task> onErrorCallback)
        {
            if (!IsListening)
            {
                IsListening = true;
            }
            return Task.CompletedTask;
        }
        public Task StopListenAsync()
        {
            IsListening = false;
            return Task.CompletedTask;
        }
        public async Task<IEnumerable<TEntity>> ReadAsync(string partitionKey, string rowKey)
            => (await Integration.ReadAsync()).Select(x => x.Message.FromJson<TEntity>());
        public async Task<bool> SendAsync(TEntity message, string partitionKey, string rowKey)
            => await Integration.SendAsync(message.ToJson());
        public async Task<bool> SendBatchAsync(IEnumerable<TEntity> messages, string partitionKey, string rowKey)
        {
            List<Task> sents = new();
            foreach (var message in messages)
                sents.Add(SendAsync(message, partitionKey, rowKey));
            await Task.WhenAll(sents);
            return true;
        }
        public async Task<long> SendScheduledAsync(TEntity message, int delayInSeconds, string partitionKey, string rowKey)
            => await Integration.SendScheduledAsync(message.ToJson(), delayInSeconds);
        public async Task<IEnumerable<long>> SendScheduledBatchAsync(IEnumerable<TEntity> messages, int delayInSeconds, string partitionKey, string rowKey)
        {
            List<Task<long>> sents = new();
            foreach (var message in messages)
                sents.Add(SendScheduledAsync(message, delayInSeconds, partitionKey, rowKey));
            await Task.WhenAll(sents);
            return sents.Select(x => x.Result);
        }
        public Task<bool> WarmUpAsync()
          => Integration.WarmUpAsync();
    }
}

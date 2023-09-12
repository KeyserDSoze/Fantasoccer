using Azure.Storage.Blobs.Models;
using Rystem.Azure.Integration.Storage;
using Rystem.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Rystem.Business
{
    internal sealed class InBlobStorage<T> : ICacheImplementation<T>
    {
        private readonly BlobStorageIntegration Integration;
        private readonly string Prefix;
        public InBlobStorage(BlobStorageIntegration integration, string prefix)
        {
            Integration = integration;
            Prefix = prefix;
        }
        private string GetKeyWithPrefix(string key)
            => $"{Prefix}/{key}";
        public async Task<T> InstanceAsync(string key)
        {
            string keyWithPrefix = GetKeyWithPrefix(key);
            var instance = await Integration.ReadAsync(keyWithPrefix, true);
            if (!string.IsNullOrWhiteSpace(instance.Properties?.CacheControl) && DateTime.UtcNow > new DateTime(long.Parse(instance.Properties.CacheControl)))
                await this.DeleteAsync(keyWithPrefix);
            else
                return (await instance.Content.ConvertToStringAsync()).FromJson<T>();
            return default;
        }
        public async Task<bool> UpdateAsync(string key, T value, TimeSpan expiringTime)
        {
            string keyWithPrefix = GetKeyWithPrefix(key);
            long expiring = expiringTime.Ticks;
            await Integration.WriteBlockAsync(keyWithPrefix, await value.ToJson().ToStreamAsync(),
                new BlobUploadOptions
                {
                    AccessTier = AccessTier.Hot,
                    HttpHeaders = new BlobHttpHeaders()
                    {
                        CacheControl = expiring > 0 ? (expiring + DateTime.UtcNow.Ticks).ToString() : string.Empty
                    }
                });
            return true;
        }

        public async Task<bool> DeleteAsync(string key)
            => await Integration.DeleteAsync(GetKeyWithPrefix(key));

        public async Task<CacheStatus<T>> ExistsAsync(string key)
        {
            string keyWithPrefix = GetKeyWithPrefix(key);
            if (await Integration.ExistsAsync(keyWithPrefix))
            {
                var value = await Integration.ReadPropertiesAsync(keyWithPrefix);
                if (!string.IsNullOrWhiteSpace(value.Properties.CacheControl) && DateTime.UtcNow > new DateTime(long.Parse(value.Properties.CacheControl)))
                {
                    await Integration.DeleteAsync(keyWithPrefix);
                    return CacheStatus<T>.NotOk();
                }
                return CacheStatus<T>.Ok();
            }
            return CacheStatus<T>.NotOk();
        }
        public async Task<IEnumerable<string>> ListAsync()
            => (await Integration.ListAsync($"{Prefix}/")).Select(x => x.Name);

        public Task<bool> WarmUpAsync() 
            => Integration.WarmUpAsync();
    }
}

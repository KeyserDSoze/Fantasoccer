using StackExchange.Redis;

namespace Rystem.Azure.Integration.Cache
{
    public sealed class RedisCacheIntegration : IWarmUp
    {
        private int RoundRobin = -1;
        private IDatabase Cache
        {
            get
            {
                int value = 0;
                if (Account.NumberOfClients > 1)
                    value = this.RoundRobin = (this.RoundRobin + 1) % Account.NumberOfClients;
                return Connections[value].Value.GetDatabase();
            }
        }
        public Task<bool> WarmUpAsync()
        {
            foreach (var connection in Connections)
                _ = connection.Value.GetDatabase();
            return Task.FromResult(true);
        }

        private readonly List<Lazy<ConnectionMultiplexer>> Connections;
        private readonly RedisCacheAccount Account;
        public RedisCacheIntegration(RedisCacheAccount account)
        {
            Account = account;
            Connections = new List<Lazy<ConnectionMultiplexer>>();
            for (int i = 0; i < account.NumberOfClients; i++)
                Connections.Add(new Lazy<ConnectionMultiplexer>(() =>
                {
                    ConnectionMultiplexer connectionMultiplexer = ConnectionMultiplexer.Connect((Account as IRystemOptions).GetConnectionStringAsync().GetAwaiter().GetResult());
                    return connectionMultiplexer;
                }));
        }
        public async Task<string> InstanceAsync(string key)
        {
            string value = await Cache.StringGetAsync(key);
            return value;
        }
        public async Task<bool> UpdateAsync(string key, string value, TimeSpan expiringTime = default)
        {
            bool code;
            if (expiringTime == default)
                expiringTime = Account.ExpiringDefault;
            if (expiringTime.Ticks > 0)
                code = await Cache.StringSetAsync(key, value, expiringTime);
            else
                code = await Cache.StringSetAsync(key, value);
            return code;
        }
        public async Task<bool> ExistsAsync(string key)
            => await Cache.KeyExistsAsync(key);
        public async Task<bool> DeleteAsync(string key)
            => await Cache.KeyDeleteAsync(key);
        public async IAsyncEnumerable<string> ListKeysAsync(string prefix)
        {
            await foreach (string redisKey in Cache.Multiplexer.GetServer(Cache.Multiplexer.GetEndPoints().First()).KeysAsync())
                if (redisKey.Contains(prefix))
                    yield return redisKey.Replace(prefix, string.Empty);
        }
        private const string FixedValue = "A";
        public async Task<bool> AcquireLockAsync(string key, TimeSpan expiringTime = default)
        {
            try
            {
                if (expiringTime == default)
                    expiringTime = TimeSpan.FromSeconds(10);
               
                return false;
            }
            catch
            {
                return false;
            }
        }
        public async Task<bool> LockIsAcquiredAsync(string key)
        {
            var value = await Cache.LockQueryAsync(new RedisKey(key));
            return value.HasValue;
        }
        public async Task<bool> ReleaseLockAsync(string key)
        {
            return true;
        }
    }
}

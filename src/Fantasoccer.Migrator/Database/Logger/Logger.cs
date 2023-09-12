using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FantacalcioV4
{
    public class Logger
    {
        public Task LogAsync<TEntity>(string message, Exception exception)
        {
            return new FantacalcioLog<TEntity>()
            {
                Error = exception?.Message ?? string.Empty,
                Message = message,
                PrimaryKey = typeof(TEntity).Name,
            }.UpdateAsync();
        }
        public Task MessageAsync<TEntity>(string message)
            => LogAsync<TEntity>(message, default);
        public Task CatchAsync<TEntity>(Exception exception)
            => LogAsync<TEntity>(string.Empty, exception);
        public Task<IEnumerable<FantacalcioLog<TEntity>>> ListTop1000Async<TEntity>() 
            => new FantacalcioLog<TEntity>().GetAsync(takeCount: 1000);
        public Task<IEnumerable<FantacalcioLog<TEntity>>> ListTop10Async<TEntity>()
            => new FantacalcioLog<TEntity>().GetAsync(takeCount: 10);
    }
}
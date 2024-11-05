using System;
using Org.BouncyCastle.Asn1.Pkcs;
using RepositoryFramework;

namespace Fantasoccer.Business
{
    internal sealed class FantasoccerLogger : IFantasoccerLogger
    {
        private readonly IRepository<FantasoccerLog, string> _repository;

        public FantasoccerLogger(IRepository<FantasoccerLog, string> repository)
        {
            _repository = repository;
        }
        public async Task LogAsync<T>(string message)
        {
            var timeDifference = DateTime.MaxValue - DateTime.UtcNow;
            var id = timeDifference.Ticks.ToString("D19");
            await _repository.InsertAsync(id, new FantasoccerLog
            {
                PrimaryKey = IFantasoccerLogger.LogPrimaryKey,
                SecondaryKey = id,
                Message = $"{typeof(T).Name} with: {message}",
            });
        }
        public async Task LogErrorAsync<T>(Exception exception)
        {
            var timeDifference = DateTime.MaxValue - DateTime.UtcNow;
            var id = timeDifference.Ticks.ToString("D19");
            await _repository.InsertAsync(id, new FantasoccerLog
            {
                PrimaryKey = IFantasoccerLogger.ErrorPrimaryKey,
                SecondaryKey = id,
                Message = $"{typeof(T).Name} with: {exception.Message}",
                Error = exception.ToString()
            });
        }

        public async Task<IEnumerable<FantasoccerLog>> Top100Async()
        {
            var top100 = await _repository.Where(x => x.PrimaryKey == IFantasoccerLogger.LogPrimaryKey).Take(100).ToListAsEntityAsync();
            return top100;
        }
        public async Task<IEnumerable<FantasoccerLog>> Top100ErrorAsync()
        {
            var top100 = await _repository.Where(x => x.PrimaryKey == IFantasoccerLogger.ErrorPrimaryKey).Take(100).ToListAsEntityAsync();
            return top100;
        }
    }
}

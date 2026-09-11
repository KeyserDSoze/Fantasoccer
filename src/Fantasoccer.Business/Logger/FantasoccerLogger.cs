using Microsoft.Extensions.Logging;
using RepositoryFramework;

namespace Fantasoccer.Business
{
    internal sealed class FantasoccerLogger : IFantasoccerLogger
    {
        private readonly IRepository<FantasoccerLog, string> _repository;
        private readonly ILogger<FantasoccerLogger>? _telemetryLogger;

        public FantasoccerLogger(
            IRepository<FantasoccerLog, string> repository,
            ILogger<FantasoccerLogger>? telemetryLogger = null)
        {
            _repository = repository;
            _telemetryLogger = telemetryLogger;
        }
        public async Task LogAsync<T>(string message)
        {
            _telemetryLogger?.LogInformation(
                "Fantasoccer business log from {Component}: {Message}",
                typeof(T).Name,
                message);

            var id = $"{IFantasoccerLogger.LogPrimaryKey}_-_{typeof(T).Name}_{DateTime.UtcNow.Hour}";
            await TryUpdateAsync(id, new FantasoccerLog
            {
                PrimaryKey = IFantasoccerLogger.LogPrimaryKey,
                SecondaryKey = id,
                Message = $"{typeof(T).Name} with: {message}",
            });
        }
        public async Task LogErrorAsync<T>(Exception exception)
        {
            _telemetryLogger?.LogError(
                exception,
                "Fantasoccer business error from {Component}",
                typeof(T).Name);

            var id = $"{IFantasoccerLogger.ErrorPrimaryKey}_-_{typeof(T).Name}_{DateTime.UtcNow.Hour}";
            await TryUpdateAsync(id, new FantasoccerLog
            {
                PrimaryKey = IFantasoccerLogger.ErrorPrimaryKey,
                SecondaryKey = id,
                Message = $"{typeof(T).Name} with: {exception.Message}",
                Error = exception.ToString()
            });
        }

        private async Task TryUpdateAsync(string id, FantasoccerLog log)
        {
            try
            {
                await _repository.UpdateAsync(id, log);
            }
            catch
            {
                // Logging must not change the outcome of the operation being logged.
            }
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

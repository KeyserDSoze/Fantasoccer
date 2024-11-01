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
            var id = Guid.NewGuid().ToString();
            await _repository.InsertAsync(id, new FantasoccerLog
            {
                PrimaryKey = typeof(T).Name,
                SecondaryKey = id,
                Message = message,
            });
        }
        public async Task LogErrorAsync<T>(Exception exception)
        {
            var id = Guid.NewGuid().ToString();
            await _repository.InsertAsync(id, new FantasoccerLog
            {
                PrimaryKey = typeof(T).Name,
                SecondaryKey = id,
                Message = exception.Message,
                Error = exception.ToString()
            });
        }

        public async Task<IEnumerable<FantasoccerLog>> Top100Async()
        {
            var top100 = await _repository.Take(100).ToListAsEntityAsync();
            return top100;
        }
    }
}

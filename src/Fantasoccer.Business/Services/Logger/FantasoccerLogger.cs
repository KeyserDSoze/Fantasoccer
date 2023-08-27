using RepositoryFramework;

namespace Fantasoccer.Business
{
    internal class FantasoccerLogger : IFantasoccerLogger
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
    }
}

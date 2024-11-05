namespace Fantasoccer
{
    public interface IFantasoccerLogger
    {
        public const string LogPrimaryKey = "ALog";
        public const string ErrorPrimaryKey = "AError";
        Task LogAsync<T>(string message);
        Task LogErrorAsync<T>(Exception exception);
        Task<IEnumerable<FantasoccerLog>> Top100Async();
        Task<IEnumerable<FantasoccerLog>> Top100ErrorAsync();
    }
}

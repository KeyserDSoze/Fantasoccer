namespace Fantasoccer
{
    public interface IFantasoccerLogger
    {
        Task LogAsync<T>(string message);
        Task LogErrorAsync<T>(Exception exception);
        Task<IEnumerable<FantasoccerLog>> Top100Async();
    }
}

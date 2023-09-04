namespace Fantasoccer
{
    public interface IFantasoccerLogger
    {
        Task LogAsync<T>(string message);
        Task LogErrorAsync<T>(Exception exception);
    }
}

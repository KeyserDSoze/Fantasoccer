namespace Fantasoccer
{
    public sealed class TeamResponse
    {
        public bool IsOk { get; }
        public string? Message { get; }
        public TeamResponse(bool isOk, string? message = null)
        {
            IsOk = isOk;
            Message = message;
        }
    }
}

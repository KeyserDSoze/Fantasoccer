namespace Fantasoccer;

public sealed class MarketOperationResult
{
    public bool IsOk { get; init; }
    public string Message { get; init; } = string.Empty;
    public Market? Market { get; init; }
}

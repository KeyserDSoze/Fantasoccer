namespace Fantasoccer.Business
{
    public sealed record OptimalFormationPlayer(
        string PlayerKey,
        string PlayerName,
        Role Role,
        FantaSoccerRole Position,
        float Projection,
        float Confidence);

    public sealed record OptimalFormationResult(
        IReadOnlyList<OptimalFormationPlayer> Players,
        string Module,
        float Score,
        float Confidence,
        IReadOnlyList<string> Warnings);

    public sealed record OptimalPlayerProjection(
        float Average = 0,
        float FantaAverage = 0,
        float RecentAverage = 0,
        float Availability = 1,
        float Matchup = 0,
        float HomeAway = 0,
        float Rank = 0,
        float Confidence = 0);

    public sealed class OptimalFormationInput
    {
        public required IReadOnlyList<Player> Players { get; init; }
        public IReadOnlyDictionary<string, OptimalPlayerProjection> Projections { get; init; } =
            new Dictionary<string, OptimalPlayerProjection>(StringComparer.OrdinalIgnoreCase);
    }
}

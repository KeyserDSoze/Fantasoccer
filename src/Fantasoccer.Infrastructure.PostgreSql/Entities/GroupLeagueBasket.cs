namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

// The legacy league's basket association is independent of a specific year.
// Opening competitions may use last year's teams before the new basket year exists.
public sealed class GroupLeagueBasket
{
    public Guid GroupId { get; set; }
    public Guid LeagueId { get; set; }
    public Guid BasketId { get; set; }
}

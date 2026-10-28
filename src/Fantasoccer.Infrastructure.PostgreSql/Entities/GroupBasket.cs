namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

public sealed class GroupBasket
{
    public Guid GroupId { get; set; }
    public Guid BasketId { get; set; }
    public required string Name { get; set; }
    public BasketKind Kind { get; set; }
    public bool IsArchived { get; set; }
    public FantasyGroup Group { get; set; } = null!;
    public ICollection<BasketSeason> Seasons { get; set; } = [];
}

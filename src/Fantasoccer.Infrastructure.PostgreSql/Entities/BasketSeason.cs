namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

public sealed class BasketSeason
{
    public Guid GroupId { get; set; }
    public Guid BasketId { get; set; }
    public int SeasonIndex { get; set; }
    public bool IsArchived { get; set; }
    public GroupBasket Basket { get; set; } = null!;
    public Season Season { get; set; } = null!;
    public ICollection<LeagueBasketSeason> Competitions { get; set; } = [];
}

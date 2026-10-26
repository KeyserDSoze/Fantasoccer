namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

public sealed class FantasyGroup
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public int Revision { get; set; } = 1;
    public ICollection<GroupMembership> Memberships { get; set; } = [];
    public ICollection<GroupBasket> Baskets { get; set; } = [];
    public ICollection<GroupLeague> Leagues { get; set; } = [];
}

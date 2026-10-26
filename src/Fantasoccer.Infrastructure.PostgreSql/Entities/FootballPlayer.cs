namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

public sealed class FootballPlayer
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public ICollection<PlayerSeason> Seasons { get; set; } = [];
}

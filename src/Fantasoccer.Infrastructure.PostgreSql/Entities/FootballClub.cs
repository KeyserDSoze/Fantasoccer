namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

public sealed class FootballClub
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string Abbreviation { get; set; } = "";
}

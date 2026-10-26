namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

public sealed class FootballPlayer
{
    private string? _id;
    public string Id { get => _id ??= KeyUtils.GetPlayerKey(Name); set => _id = value; }
    public Guid? LegacyId { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string PlayerKey => Id;
    public required string Name { get; set; }
    public ICollection<PlayerSeason> Seasons { get; set; } = [];
}

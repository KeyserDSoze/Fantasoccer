namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

public sealed class FootballClub
{
    private string? _id;
    public string Id { get => _id ??= KeyUtils.GetClubKey(Abbreviation, Name); set => _id = value; }
    public Guid? LegacyId { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string ClubCode => Id;
    public required string Name { get; set; }
    public string Abbreviation { get; set; } = "";
}

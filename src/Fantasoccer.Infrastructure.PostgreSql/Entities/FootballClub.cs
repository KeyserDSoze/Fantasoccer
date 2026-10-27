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
    public ICollection<FootballClubSeason> Seasons { get; set; } = [];

    public void NormalizeIdentity()
    {
        var code = Id;
        if (code.Length != 3 || code.Any(c => c is < 'a' or > 'z'))
            throw new InvalidOperationException("A persisted club code must already be canonical.");
        if (!string.IsNullOrWhiteSpace(Abbreviation) && (Abbreviation.Length != 3 ||
            Abbreviation.Any(c => !char.IsAsciiLetter(c)) || Abbreviation.ToLowerInvariant() != code))
            throw new InvalidOperationException("The explicit club abbreviation contradicts its key.");
        if (FantacalcioClubCodes.TryGetCode(Name, out var expected) && expected != code)
            throw new InvalidOperationException("The verified club name contradicts its key.");
        Name = FantacalcioClubCodes.CanonicalName(code, Name);
        Abbreviation = code;
    }
}

namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

public enum PlayerPosition
{
    Undefined = -1,
    Goalkeeper = 0,
    Defender = 1,
    Midfielder = 2,
    Forward = 3
}

// One shared seasonal record, regardless of how many groups use the player.
// ClubId is the current club, not a substitute for future historical transfer records.
public sealed class PlayerSeason
{
    public Guid PlayerId { get; set; }
    public int SeasonIndex { get; set; }
    // The provider's normalized name is authoritative within this season.
    // PlayerId remains the technical identity used by every foreign key.
    public required string NormalizedName { get; set; }
    public string PlayerKey { get; private set; } = null!;
    public Guid? ClubId { get; set; }
    public PlayerPosition Position { get; set; } = PlayerPosition.Undefined;
    public bool IsActive { get; set; }
    public bool IsVisible { get; set; } = true;
    public FootballPlayer Player { get; set; } = null!;
    public Season Season { get; set; } = null!;
    public FootballClub? Club { get; set; }
}

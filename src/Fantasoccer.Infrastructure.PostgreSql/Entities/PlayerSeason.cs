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
    public string PlayerId { get; set; } = null!;
    public int SeasonIndex { get; set; }
    // CLR aliases keep callers readable while the actual PK/FKs are the natural
    // (PlayerKey, SeasonIndex) pair. No concatenated yearly key is persisted.
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string PlayerKey => PlayerId;
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string NormalizedName { get => PlayerId; set => PlayerId = value; }
    public string? ClubId { get; set; }
    public PlayerPosition Position { get; set; } = PlayerPosition.Undefined;
    public bool IsActive { get; set; }
    public bool IsVisible { get; set; } = true;
    public FootballPlayer Player { get; set; } = null!;
    public Season Season { get; set; } = null!;
    public FootballClub? Club { get; set; }
}

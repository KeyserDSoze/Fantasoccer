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
// ClubId is the current club (NULL once the player left Serie A). The last move of the
// season is kept in PreviousClubId/TransferredAtUtc: at most one transfer per season is
// expected. A match's historical club comes first from that day's vote (FootballVote.ClubId).
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
    // Club before the last detected move (NULL for an arrival from outside Serie A).
    public string? PreviousClubId { get; set; }
    // When the nightly catalogue run discovered the move (not the official transfer date).
    // NULL for the season's initial snapshot and for departures recorded before tracking.
    public DateTimeOffset? TransferredAtUtc { get; set; }
    // First catalogue run that did not list the player. The departure is confirmed only on
    // a later night, so a single incomplete provider page never removes a player.
    public DateTimeOffset? MissingSinceUtc { get; set; }
    public PlayerPosition Position { get; set; } = PlayerPosition.Undefined;
    public bool IsActive { get; set; }
    public bool IsVisible { get; set; } = true;
    public FootballPlayer Player { get; set; } = null!;
    public Season Season { get; set; } = null!;
    public FootballClub? Club { get; set; }
    public FootballClub? PreviousClub { get; set; }
}

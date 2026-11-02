using Fantasoccer.Infrastructure.PostgreSql.Entities;

namespace Fantasoccer.Application.Core.Football;

// Nightly reconciliation of one season's player catalogue with the provider list.
// A player belongs to Serie A while the provider lists him; ClubId is always the
// current club, PreviousClubId/TransferredAtUtc the last detected move.
public static class PlayerTransferRules
{
    // The departure is confirmed by a later nightly run, never by a second manual run
    // on the same evening: one incomplete provider page must not remove a player.
    public static readonly TimeSpan DepartureConfirmation = TimeSpan.FromHours(20);

    public static void Listed(PlayerSeason row, string clubId, bool isNewRow, bool initialSnapshot, DateTimeOffset now)
    {
        row.MissingSinceUtc = null;
        if (isNewRow)
        {
            row.ClubId = clubId;
            // The first run of a season is the starting picture, not 600 arrivals.
            if (!initialSnapshot) { row.PreviousClubId = null; row.TransferredAtUtc = now; }
            return;
        }
        if (row.ClubId == clubId) return;
        if (row.ClubId is not null)
        {
            row.PreviousClubId = row.ClubId;
            row.TransferredAtUtc = now;
        }
        else if (row.PreviousClubId is not null)
        {
            // Back in Serie A after leaving: the previous club is the last Serie A club,
            // unless he returned to that very club.
            if (row.PreviousClubId == clubId) row.PreviousClubId = null;
            row.TransferredAtUtc = now;
        }
        row.ClubId = clubId;
    }

    public static void Missing(PlayerSeason row, DateTimeOffset now)
    {
        if (row.ClubId is null)
        {
            // Already out of Serie A, or a legacy row that never had a club.
            row.IsActive = false;
            row.MissingSinceUtc = null;
            return;
        }
        if (!row.IsActive && row.MissingSinceUtc is null)
        {
            // Removed by a catalogue run before transfers were tracked: the date is unknown.
            Leave(row, null);
            return;
        }
        if (row.MissingSinceUtc is null) { row.MissingSinceUtc = now; return; }
        if (now - row.MissingSinceUtc.Value >= DepartureConfirmation) Leave(row, row.MissingSinceUtc);
    }

    private static void Leave(PlayerSeason row, DateTimeOffset? discoveredAt)
    {
        row.PreviousClubId = row.ClubId;
        row.Club = null;
        row.ClubId = null;
        row.IsActive = false;
        row.TransferredAtUtc = discoveredAt;
        row.MissingSinceUtc = null;
    }
}

using System.Security.Claims;
using System.Text.Json;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Application.Core.Football;

public sealed class FootballApplicationService(FantasoccerDbContext database, AccountApplicationService accounts, IFootballLiveDataStore liveData, TimeProvider clock)
{
    public async Task<int[]> YearsAsync(ClaimsPrincipal caller, CancellationToken cancellationToken = default)
    {
        await accounts.CurrentAsync(caller, cancellationToken);
        return await database.FootballDatasets.Select(x => x.SeasonIndex).Distinct().OrderDescending().ToArrayAsync(cancellationToken);
    }
    public async Task<PlayersDto<FootballPlayerDto>?> PlayersAsync(ClaimsPrincipal caller, int year, CancellationToken cancellationToken = default)
    {
        await AccessAsync(caller, year, cancellationToken);
        var rows = await database.PlayerSeasons.AsNoTracking().Include(x => x.Player).Include(x => x.Club)
            .Where(x => x.SeasonIndex == year).OrderBy(x => x.Position).ThenBy(x => x.Player.Name).ToArrayAsync(cancellationToken);
        if (rows.Length == 0 && !await ExistsAsync("realplayerswrapper", year, 0, cancellationToken)) return null;
        return new(rows.Select(x => new FootballPlayerDto(x.PlayerId, x.Player.Name, x.Club is null ? null : FootballMapping.Club(x.Club),
            (int)x.Position, x.IsActive, x.IsVisible) { SeasonIndex = x.SeasonIndex }).ToArray());
    }

    public async Task<FootballPlayerDto?> PlayerByKeyAsync(ClaimsPrincipal caller, int year, string playerKey, CancellationToken cancellationToken = default)
    {
        await AccessAsync(caller, year, cancellationToken);
        if (string.IsNullOrEmpty(playerKey) || playerKey.Any(x => x is < 'a' or > 'z')) throw Invalid();
        var row = await database.PlayerSeasons.AsNoTracking().Include(x => x.Player).Include(x => x.Club)
            .SingleOrDefaultAsync(x => x.PlayerId == playerKey && x.SeasonIndex == year, cancellationToken);
        return row is null ? null : new(row.PlayerId, row.Player.Name, row.Club is null ? null : FootballMapping.Club(row.Club),
            (int)row.Position, row.IsActive, row.IsVisible) { SeasonIndex = row.SeasonIndex };
    }

    public async Task<PlayerHistoryDto?> PlayerHistoryAsync(ClaimsPrincipal caller, string playerKey, CancellationToken cancellationToken = default)
    {
        await accounts.CurrentAsync(caller, cancellationToken);
        if (string.IsNullOrEmpty(playerKey) || playerKey.Any(x => x is < 'a' or > 'z')) throw Invalid();
        var player = await database.Players.AsNoTracking().Include(x => x.Seasons).SingleOrDefaultAsync(x => x.Id == playerKey, cancellationToken);
        return player is null ? null : new(player.Id, player.Name, player.Seasons.Select(x => x.SeasonIndex).Order().ToArray());
    }

    public async Task<FootballCalendarDto?> CalendarAsync(ClaimsPrincipal caller, int year, CancellationToken cancellationToken = default)
    {
        await AccessAsync(caller, year, cancellationToken);
        return await CalendarCoreAsync(year, cancellationToken);
    }

    // Global read-only football data, used by group projections without a caller.
    internal async Task<FootballCalendarDto?> CalendarCoreAsync(int year, CancellationToken cancellationToken)
    {
        if (year is < 1 or > 200) throw Invalid();
        var days = await database.FootballMatchdays.AsNoTracking().Include(x => x.Matches).ThenInclude(x => x.HomeClub)
            .Include(x => x.Matches).ThenInclude(x => x.AwayClub).Where(x => x.SeasonIndex == year).OrderBy(x => x.SerieADay).AsSplitQuery().ToArrayAsync(cancellationToken);
        if (days.Length == 0 && !await ExistsAsync("realcalendar", year, 0, cancellationToken)) return null;
        return new(year, days.Select(day => new FootballDayDto(year, day.SerieADay, day.Matches.OrderBy(x => x.Ordinal).Select(x =>
            new FootballGameDto(x.Ordinal, FootballMapping.Club(x.HomeClub), FootballMapping.Club(x.AwayClub), x.KickoffUtc, x.HomeGoals, x.AwayGoals, x.Delayed)
            { WasPostponed = x.WasPostponed, IsFinished = x.IsFinished, OriginalDate = x.OriginalKickoffUtc }).ToArray())).ToArray());
    }

    public async Task<ClubDto[]> ClubsAsync(ClaimsPrincipal caller, CancellationToken cancellationToken = default)
    {
        await accounts.CurrentAsync(caller, cancellationToken);
        var rows = await database.Clubs.AsNoTracking().OrderBy(x => x.Name).ToArrayAsync(cancellationToken);
        return rows.Select(FootballMapping.Club).ToArray();
    }

    public async Task<ClubSeasonCatalogueDto?> SeasonClubsAsync(ClaimsPrincipal caller, int year, CancellationToken cancellationToken = default)
    {
        await AccessAsync(caller, year, cancellationToken);
        var rows = await database.ClubSeasons.AsNoTracking().Include(x => x.Club).Where(x => x.SeasonIndex == year)
            .OrderBy(x => x.Club.Name).ToArrayAsync(cancellationToken);
        var complete = await ExistsAsync(FootballClubSeason.CatalogueDatasetKind, year, 0, cancellationToken);
        return rows.Length == 0 && !complete ? null : new(year, rows.Select(x => FootballMapping.Club(x.Club)).ToArray(), complete);
    }

    public async Task<ClubHistoryDto?> ClubHistoryAsync(ClaimsPrincipal caller, string clubKey, CancellationToken cancellationToken = default)
    {
        await accounts.CurrentAsync(caller, cancellationToken);
        string code;
        try { code = KeyUtils.GetClubKey(null, clubKey); }
        catch (ArgumentException) { throw Invalid(); }
        var club = await database.Clubs.AsNoTracking().Include(x => x.Seasons).SingleOrDefaultAsync(x => x.Id == code, cancellationToken);
        return club is null ? null : new(FootballMapping.Club(club), club.Seasons.Select(x => x.SeasonIndex).Order().ToArray());
    }

    public async Task<PlayersDto<VotedPlayerDto>?> VotesAsync(ClaimsPrincipal caller, int year, int day, FootballVoteSource source, CancellationToken cancellationToken = default)
    {
        await AccessAsync(caller, year, cancellationToken); CheckDay(day);
        if (!Enum.IsDefined(source)) throw Invalid();
        if (source == FootballVoteSource.Live)
        {
            // Provisional votes: the shared cache is the only operational source.
            var snapshot = await liveData.LiveVotesAsync(year, day, cancellationToken);
            return snapshot is null ? null : new(snapshot.Payload.Players.OrderBy(x => x.PlayerRole).ThenBy(x => x.Name).Select(x =>
                new VotedPlayerDto(x.PlayerId, x.Name, FootballLiveDataMapping.ClubDto(x.ClubId, x.ClubName), x.PlayerRole, x.IsActive, x.IsVisible,
                    FootballLiveDataMapping.Dto(x.Vote)) { SeasonIndex = year }).ToArray())
                { UpdatedAtUtc = snapshot.GeneratedAtUtc, IsStale = !snapshot.IsFreshAt(clock.GetUtcNow()) };
        }
        var rows = await database.FootballVotes.AsNoTracking().Include(x => x.Player).Include(x => x.Club)
            .Where(x => x.SeasonIndex == year && x.Day == day && x.Source == source).OrderBy(x => x.PlayerRole).ThenBy(x => x.Player.Name).ToArrayAsync(cancellationToken);
        if (rows.Length == 0 && !await ExistsAsync(source.ToString().ToLowerInvariant(), year, day, cancellationToken)) return null;
        VotedPlayerDto Map(FootballVote x) => new(x.PlayerId, x.Player.Name, x.Club is null ? null : FootballMapping.Club(x.Club), x.PlayerRole, x.IsActive, x.IsVisible, FootballMapping.Vote(x)) { SeasonIndex = year };
        var legacy = source == FootballVoteSource.Official
            ? await database.LegacyPoliticalVoteEntries.AsNoTracking().Include(x => x.Club).Where(x => x.SeasonIndex == year && x.Day == day)
                .OrderBy(x => x.SourceOrdinal).ToArrayAsync(cancellationToken) : [];
        return new(rows.Select(Map).ToArray()) { LegacyPoliticalPlayers = legacy.Select(PoliticalTrace).ToArray() };
    }

    private static VotedPlayerDto PoliticalTrace(LegacyPoliticalVoteEntry row)
    {
        var player = JsonSerializer.Deserialize<VotedRealPlayer>(row.OriginalPayloadJson)
            ?? throw new InvalidOperationException("Invalid preserved political vote payload.");
        var vote = player.Vote;
        return new(row.PlayerId, player.Name, row.Club is null ? null : FootballMapping.Club(row.Club), (int)player.Role, player.IsActive, player.Visible,
            new((int)vote.Role, vote.Value, vote.IsFinal, vote.Goal, vote.Penalty, vote.Assist, vote.StoppedPenalty, vote.SufferedGoal,
                vote.WrongedPenalty, vote.OwnGoal, (int)vote.Status, vote.ManOfTheMatch, vote.HasVote, vote.IsOut, vote.IsIn, vote.Injured) { IsPolitical = true })
            { SeasonIndex = row.SeasonIndex };
    }

    public async Task<PlayersDto<ChancedPlayerDto>?> ChancesAsync(ClaimsPrincipal caller, int year, int day, CancellationToken cancellationToken = default)
    {
        await AccessAsync(caller, year, cancellationToken); CheckDay(day);
        // One global snapshot per season/day, shared by every group. Same contract as
        // before: LastGame/Trend were legacy-only fields never produced by ingestion.
        var snapshot = await liveData.ChancesAsync(year, day, cancellationToken);
        if (snapshot is null) return null;
        return new(snapshot.Payload.Players.OrderBy(x => x.Role).ThenBy(x => x.Name).Select(x => new ChancedPlayerDto(x.PlayerId, x.Name,
            FootballLiveDataMapping.ClubDto(x.ClubId, x.ClubName), x.Role, x.IsActive, x.IsVisible,
            new ChanceDto(x.Fantagazzetta, x.Gazzetta, x.Mediaset, x.Sky, x.Status, x.Description)) { SeasonIndex = year }).ToArray())
            { UpdatedAtUtc = snapshot.GeneratedAtUtc, IsStale = !snapshot.IsFreshAt(clock.GetUtcNow()) };
    }

    public async Task<PlayersDto<StatisticPlayerDto>?> StatisticsAsync(ClaimsPrincipal caller, int year, CancellationToken cancellationToken = default)
    {
        await AccessAsync(caller, year, cancellationToken);
        var rows = await database.FootballPlayerStatistics.AsNoTracking().Include(x => x.Player).Include(x => x.Club)
            .Where(x => x.SeasonIndex == year).OrderBy(x => x.Player.Name).AsSplitQuery().ToArrayAsync(cancellationToken);
        if (rows.Length == 0 && !await ExistsAsync("statplayerswrapper", year, 0, cancellationToken)) return null;
        return new(rows.Select(x => new StatisticPlayerDto(x.PlayerId, x.Player.Name, FootballMapping.Club(x.Club), x.PlayerRole, x.IsActive, x.IsVisible,
            x.Summatory, x.FantaSummatory, x.WithVote, x.WithoutVote, x.NoPlayed, x.WithSpecial, x.Goals, x.Penalties, x.Assists, x.StoppedPenalties,
            x.SufferedGoals, x.WrongedPenalties, x.OwnGoals, x.YellowCards, x.RedCards, x.EnoughVotes, x.ManOfTheMatch, x.Injured,
            x.Games.OrderBy(game => game.Ordinal).Select(game => new StatisticGameDto(game.Day, game.Vote, game.Positiveness)).ToArray(),
            x.HasDescription ? new(x.DescriptionTop, x.DescriptionDown) : null) { SeasonIndex = year }).ToArray());
    }

    public async Task<ClubStandingsDto?> RankAsync(ClaimsPrincipal caller, int year, CancellationToken cancellationToken = default)
    {
        await AccessAsync(caller, year, cancellationToken);
        var rows = await database.FootballClubRanks.AsNoTracking().Include(x => x.Club).Where(x => x.SeasonIndex == year).OrderBy(x => x.Ordinal).ToArrayAsync(cancellationToken);
        if (rows.Length == 0 && !await ExistsAsync("realrank", year, 0, cancellationToken)) return null;
        return new(rows.Select(x => new ClubRankDto(x.ClubId, x.Club.Name, x.Points, x.Victories, x.Draws, x.Defeats, x.GoalsFor, x.GoalsAgainst, x.Played)).ToArray());
    }

    public async Task SetDelayedAsync(ClaimsPrincipal caller, int year, int day, int ordinal, DelayFootballGameRequest request, CancellationToken cancellationToken = default)
    {
        await AccessAsync(caller, year, cancellationToken); CheckDay(day);
        var account = await accounts.CurrentAsync(caller, cancellationToken);
        if (!caller.IsInRole("super_admin") || !accounts.IsGlobalAdministrator(account.Email))
            throw new ApplicationFault(403, "football_admin_required", "Solo un amministratore globale può modificare il calendario condiviso.");
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var row = await database.FootballMatches.SingleOrDefaultAsync(x => x.SeasonIndex == year && x.SerieADay == day && x.Ordinal == ordinal, cancellationToken)
            ?? throw new ApplicationFault(404, "football_game_not_found", "Partita non trovata.");
        if (row.Delayed != request.ExpectedDelayed) throw new ApplicationFault(409, "football_game_changed", "Il calendario è cambiato. Ricaricare prima di salvare.");
        if (request.Delayed && !row.WasPostponed) row.OriginalKickoffUtc ??= row.KickoffUtc;
        row.WasPostponed |= row.Delayed || request.Delayed;
        row.Delayed = request.Delayed;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task AccessAsync(ClaimsPrincipal caller, int year, CancellationToken cancellationToken)
    {
        if (year is < 1 or > 200) throw Invalid();
        await accounts.CurrentAsync(caller, cancellationToken);
    }
    private Task<bool> ExistsAsync(string kind, int year, int day, CancellationToken cancellationToken) =>
        database.FootballDatasets.AnyAsync(x => x.Kind == kind && x.SeasonIndex == year && x.Day == day, cancellationToken);
    private static void CheckDay(int day) { if (day is < 1 or > 38) throw Invalid(); }
    private static ApplicationFault Invalid() => new(400, "invalid_football_context", "Stagione, giornata o sorgente non valida.");
}

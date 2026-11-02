using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Competitions;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Application.Core.Teams;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Fantasoccer.Application.Core.Football;

public sealed record FantasyOwnerDto(Guid TeamId, string Name, string Owner);

// Lineup reconstruction of a real Serie A match. Lineup is shaped like a fantasy game so the same field renders it:
// the two "teams" are the clubs, CurrentPosition is the slot (0..3 starters, 4..10 bench, 11 stands).
public sealed record FootballMatchDto(int Year, int SerieADay, FootballGameDto Game, bool IsPlaying, string HomeLineupSource, string AwayLineupSource,
    GameDto Lineup, DateTimeOffset GeneratedAtUtc)
{
    public string? FantasyLeagueName { get; init; }
}

public sealed class FootballMatchOptions
{
    // Seconds a reconstructed match stays cached in memory; 0 disables the cache.
    public int NearLiveCacheSeconds { get; set; } = 15;
    public int CacheSeconds { get; set; } = 300;
}

public sealed class FootballMatchApplicationService(FantasoccerDbContext database, AccountApplicationService accounts, IFootballLiveDataStore liveData,
    IAuthorizationService authorization, CompetitionTeamResolver teams, IMemoryCache memory, IOptions<FootballMatchOptions> options, TimeProvider clock)
{
    public async Task<FootballMatchDto> MatchAsync(ClaimsPrincipal caller, int year, int day, int ordinal, Guid? groupId, Guid? leagueId, CancellationToken cancellationToken = default)
    {
        if (year is < 1 or > 200 || day is < 1 or > 38 || ordinal < 0) throw new ApplicationFault(400, "invalid_football_context", "Stagione, giornata o partita non valida.");
        await accounts.CurrentAsync(caller, cancellationToken);
        if (groupId is { } group && !(await authorization.AuthorizeAsync(caller, group, "workspace-member")).Succeeded)
            throw new ApplicationFault(403, "football_match_group_denied", "Accesso al gruppo non consentito.");

        var match = await CachedAsync(year, day, ordinal, cancellationToken);
        if (groupId is not { } groupValue || leagueId is not { } leagueValue) return match;
        var league = await database.Leagues.AsNoTracking().SingleOrDefaultAsync(x => x.GroupId == groupValue && x.LeagueId == leagueValue, cancellationToken)
            ?? throw new ApplicationFault(404, "league_not_found", "Lega non trovata nel gruppo richiesto.");
        var owners = await OwnersAsync(groupValue, leagueValue, year, day, cancellationToken);
        if (owners.Count == 0) return match with { FantasyLeagueName = league.Name };
        LivePlayerDto Overlay(LivePlayerDto player) => owners.TryGetValue(player.Current.PlayerId, out var owner) ? player with { FantasyTeam = owner } : player;
        var sides = match.Lineup.Teams.Select(x => x with { Players = x.Players.Select(Overlay).ToArray() }).ToArray();
        return match with { Lineup = match.Lineup with { Teams = sides }, FantasyLeagueName = league.Name };
    }

    private async Task<FootballMatchDto> CachedAsync(int year, int day, int ordinal, CancellationToken cancellationToken)
    {
        var key = $"serie-a-match:{year}:{day}:{ordinal}";
        if (memory.TryGetValue(key, out FootballMatchDto? cached) && cached is not null) return cached;
        var built = await BuildAsync(year, day, ordinal, cancellationToken);
        var now = clock.GetUtcNow();
        var nearLive = built.Game.Date is not { } kickoff || (now >= kickoff.AddHours(-3) && now <= kickoff.AddHours(5));
        var seconds = nearLive ? options.Value.NearLiveCacheSeconds : options.Value.CacheSeconds;
        if (seconds > 0) memory.Set(key, built, TimeSpan.FromSeconds(seconds));
        return built;
    }

    private async Task<FootballMatchDto> BuildAsync(int year, int day, int ordinal, CancellationToken cancellationToken)
    {
        var row = await database.FootballMatches.AsNoTracking().Include(x => x.HomeClub).Include(x => x.AwayClub)
            .SingleOrDefaultAsync(x => x.SeasonIndex == year && x.SerieADay == day && x.Ordinal == ordinal, cancellationToken)
            ?? throw new ApplicationFault(404, "football_game_not_found", "Partita non trovata.");
        var now = clock.GetUtcNow();
        var kickoff = row.KickoffUtc;
        var started = kickoff is { } start && now >= start;
        string[] clubs = [row.HomeClubId, row.AwayClubId];

        var official = await database.FootballVotes.AsNoTracking().Include(x => x.Player)
            .Where(x => x.SeasonIndex == year && x.Day == day && x.Source == FootballVoteSource.Official && x.ClubId != null && clubs.Contains(x.ClubId))
            .ToArrayAsync(cancellationToken);
        var live = started && official.Length < 2 ? (await liveData.LiveVotesAsync(year, day, cancellationToken))?.Payload.Players ?? [] : [];
        var chances = (await liveData.ChancesAsync(year, day, cancellationToken))?.Payload.Players.GroupBy(x => x.PlayerId).ToDictionary(x => x.Key, x => x.First())
            ?? new Dictionary<string, CachedChance>();

        var seasons = await database.PlayerSeasons.AsNoTracking().Include(x => x.Player)
            .Where(x => x.SeasonIndex == year && x.IsVisible && x.Position != PlayerPosition.Undefined && (clubs.Contains(x.ClubId!) || clubs.Contains(x.PreviousClubId!)))
            .ToArrayAsync(cancellationToken);

        var sources = new Dictionary<string, string?>();
        var votedByClub = new Dictionary<string, (string Id, string Name, int Role, bool Active, bool Visible, VoteDto Vote)[]>();
        foreach (var club in clubs)
        {
            var fromOfficial = official.Where(x => x.ClubId == club).Select(x => (x.PlayerId, x.Player.Name, x.PlayerRole, x.IsActive, x.IsVisible, FootballMapping.Vote(x))).ToArray();
            if (fromOfficial.Length > 0) { sources[club] = LineupSources.Official; votedByClub[club] = fromOfficial; continue; }
            var fromLive = live.Where(x => x.ClubId == club).Select(x => (x.PlayerId, x.Name, x.PlayerRole, x.IsActive, x.IsVisible, FootballLiveDataMapping.Dto(x.Vote))).ToArray();
            sources[club] = fromLive.Length > 0 ? LineupSources.Live : null;
            votedByClub[club] = fromLive;
        }

        var votedIds = votedByClub.Values.SelectMany(x => x).Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        var squadByClub = clubs.Distinct().ToDictionary(club => club, club => seasons.Where(x => !votedIds.Contains(x.PlayerId) && InSquad(x, club, kickoff)).ToArray());
        var candidateIds = votedIds.Concat(squadByClub.Values.SelectMany(x => x).Select(x => x.PlayerId)).ToArray();
        var appearances = candidateIds.Length == 0 ? new Dictionary<string, int>()
            : await database.FootballVotes.AsNoTracking()
                .Where(x => x.SeasonIndex == year && x.Day < day && x.Source == FootballVoteSource.Official && candidateIds.Contains(x.PlayerId) && (x.HasVote || x.IsIn || x.IsOut))
                .GroupBy(x => x.PlayerId).Select(x => new { Id = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);

        ChanceDto? Chance(string id) => chances.TryGetValue(id, out var c) ? new ChanceDto(c.Fantagazzetta, c.Gazzetta, c.Mediaset, c.Sky, c.Status, c.Description) : null;
        int Appearances(string id) => appearances.GetValueOrDefault(id);

        SideLineup Side(string club)
        {
            var voted = votedByClub[club].Select(x => new LineupEntry(x.Id, x.Name, x.Role, x.Active, x.Visible, Appearances(x.Id), x.Vote, Chance(x.Id))).ToArray();
            var squad = squadByClub[club].Select(x => new LineupEntry(x.PlayerId, x.Player.Name, (int)x.Position, x.IsActive, x.IsVisible, Appearances(x.PlayerId), null, Chance(x.PlayerId))).ToArray();
            return SerieALineupBuilder.Build(sources[club], voted, squad);
        }

        var home = Side(row.HomeClubId);
        var away = Side(row.AwayClubId);
        var homeClub = FootballMapping.Club(row.HomeClub);
        var awayClub = FootballMapping.Club(row.AwayClub);
        var game = new FootballGameDto(row.Ordinal, homeClub, awayClub, row.KickoffUtc, row.HomeGoals, row.AwayGoals, row.Delayed)
        { WasPostponed = row.WasPostponed, IsFinished = row.IsFinished, OriginalDate = row.OriginalKickoffUtc };
        var teamsDto = new[] { Team(homeClub, home, game), Team(awayClub, away, game) };
        var calendar = new CalendarGameDto(StableId($"serie-a:{year}:{day}:{ordinal}"), ordinal, homeClub.Name, null, awayClub.Name, null, null, teamsDto[0].TeamId, teamsDto[1].TeamId);
        var lineup = new GameDto(teamsDto, calendar, day, false, false, null, null, false);
        return new(year, day, game, FootballLiveTiming.IsPlaying(row.Delayed, row.IsFinished, row.KickoffUtc, now), home.Source, away.Source, lineup, now);
    }

    // Who belonged to the club on that date: the current club unless he arrived after kickoff, or the club he left after kickoff.
    // A departure without a recorded date is never assumed.
    private static bool InSquad(PlayerSeason player, string club, DateTimeOffset? kickoff)
    {
        var date = kickoff ?? DateTimeOffset.MaxValue;
        if (player.ClubId == club) return !(player.TransferredAtUtc is { } arrived && arrived > date);
        return player.PreviousClubId == club && player.TransferredAtUtc is { } left && left > date;
    }

    private static LiveTeamDto Team(ClubDto club, SideLineup side, FootballGameDto game) =>
        new(StableId($"serie-a-club:{club.ClubId}"), Guid.Empty, 1, null, club.Name, "", [], side.Players.Select(x => Player(club, x, game)).ToArray(), null, 0, 0, 0, [], null, false);

    private static LivePlayerDto Player(ClubDto club, LineupPlacement placement, FootballGameDto game)
    {
        var entry = placement.Entry;
        var team = new FootballClubDto(club.Name, club.Abbreviation);
        var roster = new RosterPlayerDto(entry.PlayerId, 0, entry.Name, team, entry.Role, entry.IsActive, entry.IsVisible, 0, 0, 0, placement.Position);
        var final = entry.Vote is { HasVote: true } vote ? new FinalVoteDto(vote.Value, false) : null;
        return new(roster, entry.Vote, entry.Chance, game, final, placement.Position) { LineupKind = placement.Kind };
    }

    private static Guid StableId(string text) => new(MD5.HashData(Encoding.UTF8.GetBytes(text)));

    private async Task<Dictionary<string, FantasyOwnerDto>> OwnersAsync(Guid group, Guid league, int year, int day, CancellationToken cancellationToken)
    {
        var participants = await teams.ParticipantsAsync(group, league, year, false, cancellationToken);
        var owners = new Dictionary<string, FantasyOwnerDto>(StringComparer.Ordinal);
        if (participants.Length == 0) return owners;
        var authoritative = participants.Select(x => x.AuthoritativeTeamId).Distinct().ToArray();
        var snapshots = await database.RosterSnapshots.AsNoTracking()
            .Where(x => x.GroupId == group && x.SeasonIndex == year && authoritative.Contains(x.TeamId) && x.CompetitionId == Guid.Empty
                && (x.Kind == RosterKind.Season || (x.Kind == RosterKind.Matchday && x.Day <= day)))
            .Select(x => new { x.TeamId, x.Kind, x.Day, Players = x.Players.Select(p => p.PlayerId).ToList() }).ToArrayAsync(cancellationToken);
        foreach (var team in participants.OrderBy(x => x.IsDerived).ThenBy(x => x.Name, StringComparer.Ordinal))
        {
            var mine = snapshots.Where(x => x.TeamId == team.AuthoritativeTeamId).ToArray();
            var chosen = mine.Where(x => x.Kind == RosterKind.Matchday).OrderByDescending(x => x.Day).FirstOrDefault() ?? mine.FirstOrDefault(x => x.Kind == RosterKind.Season);
            if (chosen is null) continue;
            var dto = new FantasyOwnerDto(team.TeamId, team.Name, team.OwnerReference);
            foreach (var playerId in chosen.Players) owners.TryAdd(playerId, dto);
        }
        return owners;
    }
}

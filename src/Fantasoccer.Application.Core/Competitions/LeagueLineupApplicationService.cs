using System.Security.Claims;
using Fantasoccer.Application.Core.Football;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Application.Core.Teams;
using Fantasoccer.Business;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fantasoccer.Application.Core.Competitions;

public sealed record SaveLineupCommand(Guid GroupId, Guid LeagueId, int Year, int Day, Guid TeamId, FormationPositionRequest[] Positions, int ExpectedVersion,
    int? SourceVersion = null, bool Live = false, bool AsAdmin = false, Guid? MatchId = null);
public sealed record CopyLineupRequest(int Day, Guid TeamId, Guid SourceLeagueId, Guid SourceTeamId, int ExpectedVersion, int? SourceVersion = null,
    bool Live = false, bool AsAdmin = false);
public sealed record LineupCopySourceDto(Guid LeagueId, string LeagueName, Guid TeamId, string TeamName, bool HasLineup, int Version, DateTimeOffset? LastUpdateUtc);

// Effective formation of a participant in one competition and day: the
// authoritative team's roster of the day with the competition lineup applied.
internal sealed record FormationView(ResolvedTeam Team, TeamRosterDto Roster, LeagueLineup? Lineup, RosterSnapshot Source, bool IsDayRoster, string[] UnavailablePlayerIds);

public sealed class LeagueLineupApplicationService(FantasoccerDbContext database, CompetitionApplicationService competitions, CompetitionTeamResolver teams,
    ITeamChecker checker, IOptions<AssetLinkOptions> assets, TimeProvider clock)
{
    private const int Tribune = (int)FantaSoccerRole.Tribune;

    internal async Task<FormationView?> ReadAsync(Guid group, Guid league, int year, ResolvedTeam team, int day, bool allowCurrentRoster, CancellationToken cancellationToken, bool proposePrevious = false)
    {
        var source = await Rosters(group, year, team.AuthoritativeTeamId).AsNoTracking()
            .SingleOrDefaultAsync(x => x.Kind == RosterKind.Matchday && x.Day == day && x.CompetitionId == Guid.Empty, cancellationToken);
        var isDay = source is not null;
        if (source is null && allowCurrentRoster)
            source = await Rosters(group, year, team.AuthoritativeTeamId).AsNoTracking().SingleOrDefaultAsync(x => x.Kind == RosterKind.Season && x.Day == 0, cancellationToken);
        if (source is null) return null;
        var lineup = await Lineups(group, league, year, team.TeamId, day).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        // A day without a lineup of its own (and without starters in the shared roster)
        // proposes the last lineup of this team in this competition. Nothing is stored:
        // the proposal becomes the lineup of the day only when the user saves it.
        LeagueLineup? proposal = null;
        if (proposePrevious && lineup is null && !source.Players.Any(x => x.Status == 0 && x.Position != Tribune))
            proposal = await database.LeagueLineups.AsNoTracking().Where(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year && x.TeamId == team.TeamId && x.Day < day)
                .OrderByDescending(x => x.Day).FirstOrDefaultAsync(cancellationToken);
        return View(group, league, year, day, team, source, isDay, lineup, proposal);
    }

    public async Task<FormationSavedDto> SaveAsync(ClaimsPrincipal caller, SaveLineupCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var result = await SaveCoreAsync(caller, command, cancellationToken);
        await DatabaseFaults.GuardAsync(() => database.SaveChangesAsync(cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return result();
    }

    // Owner/basket based compatibility path of PUT /api/teams/formation (kind = matchday).
    public async Task<FormationSavedDto> SaveRosterRequestAsync(ClaimsPrincipal caller, SaveRosterFormationRequest request, CancellationToken cancellationToken = default)
    {
        if (request.LeagueId is not { } league) throw Invalid("Le formazioni giornaliere appartengono a una competizione: specificare la lega.");
        await competitions.AuthorizeAsync(caller, request.GroupId, league, request.Year, cancellationToken);
        var participants = await teams.ParticipantsAsync(request.GroupId, league, request.Year, cancellationToken: cancellationToken);
        var team = participants.SingleOrDefault(x => request.TeamId is { } id ? x.TeamId == id
                : x.BasketId == request.BasketId && string.Equals(x.OwnerReference, request.Owner.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new ApplicationFault(404, "team_not_found", "Squadra non trovata nella competizione.");
        return await SaveAsync(caller, new(request.GroupId, league, request.Year, request.Day, team.TeamId, request.Positions, request.ExpectedVersion,
            request.SourceVersion, request.Live, request.AsAdmin, request.MatchId), cancellationToken);
    }

    // Copy is a one-off transfer of positions between two competitions using the
    // same authoritative roster. The target goes through every save validation.
    public async Task<FormationSavedDto> CopyAsync(ClaimsPrincipal caller, Guid group, Guid league, int year, CopyLineupRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Day is < 1 or > 38) throw Invalid("Giornata non valida.");
        await competitions.AuthorizeAsync(caller, group, request.SourceLeagueId, year, cancellationToken);
        if (request.SourceLeagueId == league && request.SourceTeamId == request.TeamId) throw Invalid("Origine e destinazione coincidono.");
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var resolved = await teams.ResolveManyAsync(group, year, [request.SourceTeamId, request.TeamId], cancellationToken);
        var source = resolved.SingleOrDefault(x => x.TeamId == request.SourceTeamId) ?? throw new ApplicationFault(404, "team_not_found", "Squadra di origine non trovata.");
        var target = resolved.SingleOrDefault(x => x.TeamId == request.TeamId) ?? throw new ApplicationFault(404, "team_not_found", "Squadra di destinazione non trovata.");
        if (source.AuthoritativeTeamId != target.AuthoritativeTeamId)
            throw new ApplicationFault(409, "lineup_copy_roster_mismatch", "Si può copiare soltanto fra competizioni che condividono la stessa rosa.");
        await RequireParticipationAsync(group, request.SourceLeagueId, year, source.TeamId, cancellationToken);
        var view = await ReadAsync(group, request.SourceLeagueId, year, source, request.Day, true, cancellationToken)
            ?? throw new ApplicationFault(404, "roster_not_found", "Formazione di origine non disponibile.");
        var positions = view.Roster.Players.Where(x => x.Status == 0).Select(x => new FormationPositionRequest(x.PlayerId, x.Position)).ToArray();
        var result = await SaveCoreAsync(caller, new(group, league, year, request.Day, target.TeamId, positions, request.ExpectedVersion, request.SourceVersion,
            request.Live, request.AsAdmin), cancellationToken);
        await DatabaseFaults.GuardAsync(() => database.SaveChangesAsync(cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return result() with { Message = "Formazione copiata." };
    }

    public async Task<LineupCopySourceDto[]> CopySourcesAsync(ClaimsPrincipal caller, Guid group, Guid league, int year, int day, Guid teamId, CancellationToken cancellationToken = default)
    {
        await competitions.AuthorizeAsync(caller, group, league, year, cancellationToken);
        var target = await teams.ResolveAsync(group, year, teamId, cancellationToken) ?? throw new ApplicationFault(404, "team_not_found", "Squadra non trovata.");
        var candidates = await database.LeagueParticipations.AsNoTracking().Where(x => x.GroupId == group && x.SeasonIndex == year && !(x.LeagueId == league && x.TeamId == teamId))
            .Join(database.Leagues.Where(x => !x.IsArchived), x => new { x.GroupId, x.LeagueId }, l => new { l.GroupId, l.LeagueId }, (x, l) => new { x.LeagueId, x.TeamId, l.Name })
            .ToArrayAsync(cancellationToken);
        var resolved = (await teams.ResolveManyAsync(group, year, candidates.Select(x => x.TeamId).ToArray(), cancellationToken)).ToDictionary(x => x.TeamId);
        var result = new List<LineupCopySourceDto>();
        foreach (var candidate in candidates.Where(x => resolved.TryGetValue(x.TeamId, out var team) && team.AuthoritativeTeamId == target.AuthoritativeTeamId))
        {
            var lineup = await Lineups(group, candidate.LeagueId, year, candidate.TeamId, day).AsNoTracking().Select(x => new { x.Version, x.LastUpdateUtc }).SingleOrDefaultAsync(cancellationToken);
            result.Add(new(candidate.LeagueId, candidate.Name, candidate.TeamId, resolved[candidate.TeamId].Name, lineup is not null, lineup?.Version ?? 0, lineup?.LastUpdateUtc));
        }
        return result.OrderBy(x => x.LeagueName).ToArray();
    }

    // Players used to compute a result: the day roster (penalised if never
    // submitted, as before) with the competition lineup applied.
    internal async Task<RosterPlayer[]> ScoringPlayersAsync(Guid group, Guid league, int year, int day, Guid teamId, CancellationToken cancellationToken)
    {
        var team = await teams.ResolveAsync(group, year, teamId, cancellationToken) ?? throw new ApplicationFault(409, "roster_missing_for_recalculation", "Squadra assente: ricalcolo annullato.");
        var daily = await DayRosterAsync(group, year, day, team.AuthoritativeTeamId, cancellationToken);
        var lineup = await Lineups(group, league, year, teamId, day).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return Overlay(daily.Players, lineup);
    }

    internal async Task<RosterSnapshot> DayRosterAsync(Guid group, int year, int day, Guid authoritativeTeam, CancellationToken cancellationToken)
    {
        var daily = database.RosterSnapshots.Local.SingleOrDefault(x => x.GroupId == group && x.SeasonIndex == year && x.TeamId == authoritativeTeam && x.Kind == RosterKind.Matchday && x.Day == day && x.CompetitionId == Guid.Empty)
            ?? await Rosters(group, year, authoritativeTeam).SingleOrDefaultAsync(x => x.Kind == RosterKind.Matchday && x.Day == day && x.CompetitionId == Guid.Empty, cancellationToken);
        if (daily is not null) return daily;
        var source = await Rosters(group, year, authoritativeTeam).SingleOrDefaultAsync(x => x.Kind == RosterKind.Season && x.Day == 0, cancellationToken)
            ?? throw new ApplicationFault(409, "roster_missing_for_recalculation", "Rosa server assente: ricalcolo annullato.");
        daily = TeamRosterApplicationService.Copy(source, RosterKind.Matchday, day, null);
        // Preserve the existing explicit-recalculation penalty for unsubmitted
        // formations, never copy today's starting lineup into a historical day.
        foreach (var player in daily.Players.Where(x => x.Status == 0)) player.Position = Tribune;
        daily.LastUpdateUtc = clock.GetUtcNow();
        database.RosterSnapshots.Add(daily);
        return daily;
    }

    internal static RosterPlayer[] Overlay(IEnumerable<RosterPlayer> players, LeagueLineup? lineup)
    {
        var positions = lineup?.Players.ToDictionary(x => x.PlayerId, x => x.Position, StringComparer.Ordinal);
        return players.OrderBy(x => x.Ordinal).Select(x => new RosterPlayer { Ordinal = x.Ordinal, PlayerId = x.PlayerId, Player = x.Player, ClubId = x.ClubId, Club = x.Club,
            PlayerRole = x.PlayerRole, IsActive = x.IsActive, IsVisible = x.IsVisible, Price = x.Price, Revenue = x.Revenue, Status = x.Status,
            // A lineup never silently gains a new player: anyone bought after it was saved sits in the stands.
            Position = positions is null || x.Status != 0 ? x.Position : positions.GetValueOrDefault(x.PlayerId, Tribune) }).ToArray();
    }

    private async Task<Func<FormationSavedDto>> SaveCoreAsync(ClaimsPrincipal caller, SaveLineupCommand command, CancellationToken cancellationToken)
    {
        if (command.Day is < 1 or > 38) throw Invalid("Giornata non valida.");
        var season = await competitions.AuthorizeAsync(caller, command.GroupId, command.LeagueId, command.Year, cancellationToken);
        var email = caller.FindFirstValue(ClaimTypes.Email)!.Trim().ToLowerInvariant();
        var actor = await database.GroupMemberships.Where(x => x.GroupId == command.GroupId && x.User.Email == email && x.User.IsEnabled)
            .Select(x => new { x.UserId, x.User.DisplayName, x.Roles }).SingleAsync(cancellationToken);
        var team = await teams.ResolveAsync(command.GroupId, command.Year, command.TeamId, cancellationToken)
            ?? throw new ApplicationFault(404, "team_not_found", "Squadra non trovata.");
        await RequireParticipationAsync(command.GroupId, command.LeagueId, command.Year, team.TeamId, cancellationToken);
        // SuperAdmin acting for another user still operates on the selected team.
        var admin = command.AsAdmin && actor.Roles.HasFlag(GroupRole.SuperAdmin);
        if (team.IsArchived || season.IsArchived || (!team.IsControlledBy(actor.UserId) && !admin)) throw Denied();
        if (command.MatchId is { } matchId && !await database.FantasyMatches.AnyAsync(x => x.GroupId == command.GroupId && x.LeagueId == command.LeagueId &&
            x.SeasonIndex == command.Year && x.Id == matchId && x.Matchday.SerieADay == command.Day && (x.HomeTeamId == team.TeamId || x.AwayTeamId == team.TeamId), cancellationToken))
            throw Invalid("La squadra o la giornata non appartiene alla partita richiesta.");
        var rules = await database.LeagueRules.AsNoTracking().SingleOrDefaultAsync(x => x.GroupId == command.GroupId && x.LeagueId == command.LeagueId && x.SeasonIndex == command.Year, cancellationToken)
            ?? throw new ApplicationFault(409, "formation_rules_unavailable", "Regolamento non disponibile.");
        var lineup = await Lineups(command.GroupId, command.LeagueId, command.Year, team.TeamId, command.Day).SingleOrDefaultAsync(cancellationToken);
        var daily = await Rosters(command.GroupId, command.Year, team.AuthoritativeTeamId)
            .SingleOrDefaultAsync(x => x.Kind == RosterKind.Matchday && x.Day == command.Day && x.CompetitionId == Guid.Empty, cancellationToken);
        var now = clock.GetUtcNow();
        if (!admin)
        {
            var kickoffs = await database.FootballMatches.Where(x => x.SeasonIndex == command.Year && x.SerieADay == command.Day && (x.WasPostponed ? x.OriginalKickoffUtc : x.KickoffUtc) != null)
                .Select(x => (x.WasPostponed ? x.OriginalKickoffUtc : x.KickoffUtc)!.Value).ToArrayAsync(cancellationToken);
            if (kickoffs.Length == 0) throw new ApplicationFault(409, "matchday_schedule_unavailable", "Calendario della giornata non disponibile.");
            if (command.Live)
            {
                if (now < kickoffs.Min() || now > kickoffs.Max().AddHours(4) || (daily is null && lineup is null))
                    throw new ApplicationFault(409, "live_formation_closed", "La finestra delle modifiche live non è aperta.");
                var used = lineup?.FormationChanges ?? daily?.FormationChanges ?? 0;
                if (rules.LiveFormationChanges <= 0 || used >= rules.LiveFormationChanges)
                    throw new ApplicationFault(409, "live_changes_exhausted", "Modifiche live non abilitate o esaurite.");
            }
            else if (now >= kickoffs.Min()) throw new ApplicationFault(409, "formation_deadline_passed", "La giornata è già iniziata.");
        }
        if (lineup is null)
        {
            if (command.ExpectedVersion != 0) throw Changed();
            var baseline = daily ?? await Rosters(command.GroupId, command.Year, team.AuthoritativeTeamId).SingleOrDefaultAsync(x => x.Kind == RosterKind.Season && x.Day == 0, cancellationToken)
                ?? throw new ApplicationFault(404, "roster_not_found", "Rosa di base non trovata.");
            if (command.SourceVersion != baseline.Version) throw Changed();
        }
        else if (command.ExpectedVersion != lineup.Version) throw Changed();
        if (daily is null)
        {
            // Freeze the authoritative roster of the day (shared by every competition
            // of that team); positions there remain only the legacy default.
            var season0 = await Rosters(command.GroupId, command.Year, team.AuthoritativeTeamId).SingleAsync(x => x.Kind == RosterKind.Season && x.Day == 0, cancellationToken);
            daily = TeamRosterApplicationService.Copy(season0, RosterKind.Matchday, command.Day, null);
            daily.FormationChanges = 0; daily.FormationHistory.Clear(); daily.LastUpdateUtc = now;
            database.RosterSnapshots.Add(daily);
        }
        var active = daily.Players.Where(x => x.Status == 0).ToArray();
        if (command.Positions is null || command.Positions.Select(x => x.PlayerId).Distinct().Count() != command.Positions.Length ||
            command.Positions.Any(x => x.Position is < 0 or > 11) || !active.Select(x => x.PlayerId).ToHashSet().SetEquals(command.Positions.Select(x => x.PlayerId)))
            throw Invalid("Specificare soltanto le posizioni di tutti i giocatori attivi della rosa server.");
        var positions = command.Positions.ToDictionary(x => x.PlayerId, x => x.Position);
        var current = Overlay(active, lineup).ToDictionary(x => x.PlayerId, x => x.Position);
        var changed = active.Where(x => current[x.PlayerId] != positions[x.PlayerId]).ToArray();
        if (command.Live && !admin && !rules.AllowLiveModuleChange &&
            (changed.Length != 2 || changed[0].PlayerRole != changed[1].PlayerRole || current[changed[0].PlayerId] != positions[changed[1].PlayerId] || current[changed[1].PlayerId] != positions[changed[0].PlayerId]))
            throw Invalid("Sono consentiti solo cambi fra due giocatori dello stesso ruolo.");
        var candidate = new Team { Name = team.Name, Owner = team.OwnerReference, AdditionalOwners = [], Players = active.Select(x => new Player
        {
            Name = x.Player.Name, Role = (Role)x.PlayerRole, RealTeam = FootballMapping.DomainClubOrEmpty(x.Club),
            Status = PlayerInTeamStatus.Active, Position = (FantaSoccerRole)positions[x.PlayerId]
        }).ToList() };
        var validation = checker.Validate(candidate);
        if (!validation.IsOk) throw Invalid(validation.Message ?? "Formazione non valida.");
        if (lineup is null)
        {
            lineup = new LeagueLineup { GroupId = command.GroupId, LeagueId = command.LeagueId, SeasonIndex = command.Year, TeamId = team.TeamId, Day = command.Day,
                // A day whose shared legacy formation already used live changes keeps counting them.
                FormationChanges = command.Live ? daily.FormationChanges : 0, LastUpdateUtc = now, UpdatedByUserId = actor.UserId };
            database.LeagueLineups.Add(lineup);
        }
        var rewritten = changed.Length > 0 || !lineup.Players.Select(x => x.PlayerId).ToHashSet(StringComparer.Ordinal).SetEquals(active.Select(x => x.PlayerId));
        if (database.Entry(lineup).State != EntityState.Added && rewritten) { lineup.Version++; lineup.LastUpdateUtc = now; lineup.UpdatedByUserId = actor.UserId; }
        if (rewritten)
        {
            if (command.Live && changed.Length > 0) lineup.FormationChanges++;
            if (changed.Length > 0)
                lineup.History.Add(new() { Ordinal = lineup.History.Count, Author = actor.DisplayName, DateUtc = now,
                    Players = changed.Select((player, index) => new LeagueLineupChangePlayer { Ordinal = index, PlayerName = player.Player.Name,
                        FromPosition = current[player.PlayerId], ToPosition = positions[player.PlayerId] }).ToList() });
            lineup.Players.Clear();
            if (database.Entry(lineup).State != EntityState.Added) await database.SaveChangesAsync(cancellationToken);
            foreach (var player in active) lineup.Players.Add(new() { PlayerId = player.PlayerId, Position = positions[player.PlayerId] });
        }
        var saved = lineup; var roster = daily; var resolved = team;
        return () => new(true, "Formazione salvata.", View(command.GroupId, command.LeagueId, command.Year, command.Day, resolved, roster, true, saved).Roster);
    }

    private FormationView View(Guid group, Guid league, int year, int day, ResolvedTeam team, RosterSnapshot source, bool isDay, LeagueLineup? lineup, LeagueLineup? proposal = null)
    {
        var players = Overlay(source.Players, lineup ?? proposal);
        var active = source.Players.Where(x => x.Status == 0).Select(x => x.PlayerId).ToHashSet(StringComparer.Ordinal);
        var unavailable = lineup?.Players.Where(x => !active.Contains(x.PlayerId)).Select(x => x.PlayerId).Order().ToArray() ?? [];
        var history = lineup is not null
            ? lineup.History.OrderBy(x => x.Ordinal).Select(x => new FormationChangeDto(x.DateUtc.ToString("O"), x.Author,
                x.Players.OrderBy(p => p.Ordinal).Select(p => new FormationPlayerChangeDto(p.PlayerName, p.FromPosition, p.ToPosition)).ToArray())).ToArray()
            : isDay ? source.FormationHistory.OrderBy(x => x.Ordinal).Select(x => new FormationChangeDto(x.DateUtc?.ToString("O") ?? x.LegacyDateLocal?.ToString("s"), x.Author,
                x.Players.OrderBy(p => p.Ordinal).Select(p => new FormationPlayerChangeDto(p.PlayerName, p.FromPosition, p.ToPosition)).ToArray())).ToArray() : [];
        var roster = new TeamRosterDto(team.TeamId, group, team.BasketId, year, (int)RosterKind.Matchday, day, league, team.Name, team.OwnerReference, team.AdditionalOwners,
            players.Select(x => new RosterPlayerDto(x.PlayerId, x.Ordinal, x.Player.Name, FootballClubDto.Of(x.Club), x.PlayerRole, x.IsActive, x.IsVisible,
                x.Price, x.Revenue, x.Status, x.Position) { SeasonIndex = year }).ToArray(),
            source.MoneyFromRank, source.OpeningCompetitionPrize, (lineup?.LastUpdateUtc ?? source.LastUpdateUtc)?.ToString("O") ?? source.LegacyLastUpdateLocal?.ToString("s"),
            lineup?.FormationChanges ?? (isDay ? source.FormationChanges : 0), history, lineup?.Version ?? 0, AssetLinks.Logo(assets.Value, team.Logo),
            lineup is null ? source.Version : null, source.MarketBalanceAdjustment, source.RankCreditGranted) { UnavailablePlayerIds = unavailable, ProposedFromDay = proposal?.Day };
        return new(team, roster, lineup, source, isDay, unavailable);
    }

    private async Task RequireParticipationAsync(Guid group, Guid league, int year, Guid team, CancellationToken cancellationToken)
    {
        if (!await database.LeagueParticipations.AnyAsync(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year && x.TeamId == team, cancellationToken))
            throw new ApplicationFault(404, "team_not_in_competition", "La squadra non partecipa alla competizione.");
    }

    private IQueryable<RosterSnapshot> Rosters(Guid group, int year, Guid team) => database.RosterSnapshots.Include(x => x.Players).ThenInclude(x => x.Player)
        .Include(x => x.Players).ThenInclude(x => x.Club).Where(x => x.GroupId == group && x.SeasonIndex == year && x.TeamId == team).AsSplitQuery();

    private IQueryable<LeagueLineup> Lineups(Guid group, Guid league, int year, Guid team, int day) =>
        database.LeagueLineups.Where(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year && x.TeamId == team && x.Day == day);

    private static ApplicationFault Denied() => new(403, "team_access_denied", "Accesso alla squadra non consentito.");
    private static ApplicationFault Invalid(string message) => new(400, "invalid_formation", message);
    private static ApplicationFault Changed() => new(409, "roster_changed", "La rosa o la formazione è cambiata. Ricaricare prima di salvare.");
}

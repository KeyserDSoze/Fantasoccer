using System.Security.Claims;
using Fantasoccer.Business;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fantasoccer.Application.Core.Teams;

public sealed class TeamRosterApplicationService(FantasoccerDbContext database, IAuthorizationService authorization,
    ITeamChecker checker, TimeProvider clock, IOptions<AssetLinkOptions> assets)
{
    public async Task<TeamRosterDto?> GetAsync(ClaimsPrincipal caller, Guid groupId, Guid basketId, int year, string owner,
        RosterKind kind = RosterKind.Season, int day = 0, Guid? leagueId = null, CancellationToken cancellationToken = default, Guid? teamId = null)
    {
        await AuthorizeAsync(caller, groupId);
        if (!Enum.IsDefined(kind) || day is < 0 or > 38 || (kind is RosterKind.Season or RosterKind.OpeningSeason) != (day == 0))
            throw Invalid("Contesto rosa non valido.");
        var competition = kind is RosterKind.OpeningSeason or RosterKind.OpeningMatchday ? leagueId : null;
        if ((kind is RosterKind.OpeningSeason or RosterKind.OpeningMatchday) && competition is null) throw Invalid("Specificare la competizione iniziale.");
        var roster = await Query(groupId, basketId, year, owner, kind, day, competition, teamId).SingleOrDefaultAsync(cancellationToken);
        return roster is null ? null : await DtoAsync(roster, cancellationToken);
    }

    public async Task<FormationSavedDto> SaveFormationAsync(ClaimsPrincipal caller, SaveRosterFormationRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        await AuthorizeAsync(caller, request.GroupId);
        var email = caller.FindFirstValue(ClaimTypes.Email)!.Trim().ToLowerInvariant();
        var actor = await database.GroupMemberships.Where(x => x.GroupId == request.GroupId && x.User.Email == email && x.User.IsEnabled)
            .Select(x => new { x.UserId, x.User.DisplayName, x.Roles }).SingleAsync(cancellationToken);
        var kind = (RosterKind)request.Kind;
        if (!Enum.IsDefined(kind) || request.Day is < 0 or > 38 || (kind is RosterKind.Season or RosterKind.OpeningSeason) != (request.Day == 0))
            throw Invalid("Contesto formazione non valido.");
        var competition = kind is RosterKind.OpeningSeason or RosterKind.OpeningMatchday ? request.LeagueId : null;
        if ((kind is RosterKind.OpeningSeason or RosterKind.OpeningMatchday) && competition is null) throw Invalid("Specificare la competizione iniziale.");
        var canonical = request.Owner.Trim().ToLowerInvariant();
        var opening = kind is RosterKind.OpeningSeason or RosterKind.OpeningMatchday;
        var originYear = opening ? request.Year - 1 : request.Year;
        var team = await database.TeamSeasons.Include(x => x.Owners).Where(x => x.GroupId == request.GroupId && x.BasketId == request.BasketId &&
            x.SeasonIndex == originYear && !x.IsArchived && (request.TeamId != null ? x.Id == request.TeamId : x.Owners.Any(owner => owner.IsPrimary && owner.Membership.User.Email == canonical)))
            .SingleOrDefaultAsync(cancellationToken) ?? throw new ApplicationFault(404, "team_not_found", "Squadra non trovata.");
        var admin = request.AsAdmin && actor.Roles.HasFlag(GroupRole.SuperAdmin);
        if (!team.Owners.Any(x => x.UserId == actor.UserId) && !admin) throw Denied();
        if (request.MatchId is { } matchId && !await database.FantasyMatches.AnyAsync(x => x.GroupId == request.GroupId && x.LeagueId == request.LeagueId &&
            x.SeasonIndex == request.Year && x.Id == matchId && x.Matchday.SerieADay == request.Day && (x.HomeTeamId == team.Id || x.AwayTeamId == team.Id), cancellationToken))
            throw Invalid("La squadra o la giornata non appartiene alla partita richiesta.");
        var roster = await Query(request.GroupId, request.BasketId, request.Year, canonical, kind, request.Day, competition, team.Id).SingleOrDefaultAsync(cancellationToken);
        var rules = await RulesAsync(team, request.Year, request.LeagueId, opening, cancellationToken);
        if (opening && (!rules.OpeningCompetitionEnabled || request.Day > rules.OpeningCompetitionSerieADays || request.Live))
            throw Invalid("Competizione iniziale non abilitata, giornata non valida o modifica live non supportata.");
        var now = clock.GetUtcNow();
        if (request.Day > 0 && !admin)
        {
            var kickoffs = await database.FootballMatches.Where(x => x.SeasonIndex == request.Year && x.SerieADay == request.Day && !x.Delayed && x.KickoffUtc != null)
                .Select(x => x.KickoffUtc!.Value).ToArrayAsync(cancellationToken);
            if (kickoffs.Length == 0) throw new ApplicationFault(409, "matchday_schedule_unavailable", "Calendario della giornata non disponibile.");
            if (request.Live)
            {
                if (now < kickoffs.Min() || now > kickoffs.Max().AddHours(4) || roster is null)
                    throw new ApplicationFault(409, "live_formation_closed", "La finestra delle modifiche live non è aperta.");
                if (rules.LiveFormationChanges <= 0 || roster.FormationChanges >= rules.LiveFormationChanges)
                    throw new ApplicationFault(409, "live_changes_exhausted", "Modifiche live non abilitate o esaurite.");
            }
            else if (now >= kickoffs.Min()) throw new ApplicationFault(409, "formation_deadline_passed", "La giornata è già iniziata.");
        }
        if (request.Live && request.Day == 0) throw Invalid("Le modifiche live richiedono una giornata.");
        if (roster is null)
        {
            if (request.ExpectedVersion != 0) throw Changed();
            var baselineKind = kind == RosterKind.OpeningMatchday ? RosterKind.OpeningSeason : RosterKind.Season;
            var baseline = await Query(request.GroupId, request.BasketId, request.Year, canonical, baselineKind, 0, competition, team.Id)
                .SingleOrDefaultAsync(cancellationToken) ?? throw new ApplicationFault(404, "roster_not_found", "Rosa di base non trovata.");
            if (request.SourceVersion != baseline.Version) throw Changed();
            roster = Copy(baseline, kind, request.Day, competition);
            roster.Name = team.Name;
            roster.OwnerUserId = team.Owners.Single(x => x.IsPrimary).UserId;
            var coOwners = team.Owners.Where(x => !x.IsPrimary).Select(x => x.UserId).ToArray();
            var users = await database.Users.Where(x => coOwners.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
            roster.AdditionalOwners = coOwners.Select(id => new RosterOwner { UserId = id, User = users[id] }).ToList();
            database.RosterSnapshots.Add(roster);
        }
        else if (request.ExpectedVersion != roster.Version) throw Changed();
        var active = roster.Players.Where(x => x.Status == 0).ToArray();
        if (request.Positions is null || request.Positions.Select(x => x.PlayerId).Distinct().Count() != request.Positions.Length ||
            request.Positions.Any(x => x.Position is < 0 or > 11) || !active.Select(x => x.PlayerId).ToHashSet().SetEquals(request.Positions.Select(x => x.PlayerId)))
            throw Invalid("Specificare soltanto le posizioni di tutti i giocatori attivi della rosa server.");
        var positions = request.Positions.ToDictionary(x => x.PlayerId, x => x.Position);
        var changed = active.Where(x => x.Position != positions[x.PlayerId]).ToArray();
        if (request.Live && !admin && !rules.AllowLiveModuleChange &&
            (changed.Length != 2 || changed[0].PlayerRole != changed[1].PlayerRole || changed[0].Position != positions[changed[1].PlayerId] || changed[1].Position != positions[changed[0].PlayerId]))
            throw Invalid("Sono consentiti solo cambi fra due giocatori dello stesso ruolo.");
        var candidate = new Team { Name = roster.Name, Owner = canonical, AdditionalOwners = [], Players = active.Select(x => new Player
        {
            Name = x.Player.Name, Role = (Role)x.PlayerRole, RealTeam = new() { Name = x.Club.Name, Abbreviation = x.Club.Abbreviation },
            Status = PlayerInTeamStatus.Active, Position = (FantaSoccerRole)positions[x.PlayerId]
        }).ToList() };
        var validation = checker.Validate(candidate);
        if (!validation.IsOk) throw Invalid(validation.Message ?? "Formazione non valida.");
        if (changed.Length > 0)
        {
            if (request.Live) roster.FormationChanges++;
            roster.FormationHistory.Add(new() { Ordinal = roster.FormationHistory.Count, Author = actor.DisplayName, DateUtc = now,
                Players = changed.Select((player, index) => new RosterPlayerChange { Ordinal = index, PlayerName = player.Player.Name,
                    FromPosition = player.Position, ToPosition = positions[player.PlayerId] }).ToList() });
            foreach (var player in active) player.Position = positions[player.PlayerId];
            roster.LastUpdateUtc = now; roster.Version++;
        }
        else if (database.Entry(roster).State == EntityState.Added) roster.LastUpdateUtc = now;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true, "Formazione salvata.", await DtoAsync(roster, cancellationToken));
    }

    private IQueryable<RosterSnapshot> Query(Guid group, Guid basket, int year, string owner, RosterKind kind, int day, Guid? competition, Guid? teamId = null)
    {
        var email = owner.Trim().ToLowerInvariant();
        var competitionId = competition ?? Guid.Empty;
        var query = database.RosterSnapshots.Include(x => x.Players).ThenInclude(x => x.Player)
            .Include(x => x.Players).ThenInclude(x => x.Club).Include(x => x.AdditionalOwners).ThenInclude(x => x.User)
            .Where(x => x.GroupId == group && x.BasketId == basket && x.SeasonIndex == year && x.Kind == kind && x.Day == day && x.CompetitionId == competitionId);
        if (teamId is { } id) query = query.Where(x => x.TeamId == id);
        else if (UserAccount.TryOwnerReference(owner, out var userId))
        {
            query = kind is RosterKind.Season or RosterKind.OpeningSeason
                ? query.Where(x => database.TeamOwnerships.Any(ownership => ownership.GroupId == group && ownership.TeamId == x.TeamId && ownership.IsPrimary && ownership.UserId == userId))
                : query.Where(x => x.OwnerUserId == userId);
        }
        else if (kind is RosterKind.Season or RosterKind.OpeningSeason)
            query = query.Where(x => database.TeamOwnerships.Any(ownership => ownership.GroupId == group && ownership.TeamId == x.TeamId && ownership.IsPrimary && ownership.Membership.User.Email == email));
        else query = query.Where(x => database.Users.Any(user => user.Id == x.OwnerUserId && user.Email == email));
        return query.AsSplitQuery();
    }

    internal async Task<LeagueRules> RulesAsync(FantasyTeamSeason team, int competitionYear, Guid? contextLeague, bool opening, CancellationToken cancellationToken)
    {
        if (opening)
        {
            if (contextLeague is null || !await database.GroupLeagueBaskets.AnyAsync(x => x.GroupId == team.GroupId && x.LeagueId == contextLeague && x.BasketId == team.BasketId, cancellationToken) ||
                !await database.Leagues.AnyAsync(x => x.GroupId == team.GroupId && x.LeagueId == contextLeague && x.IsMain && !x.IsArchived, cancellationToken)) throw Denied();
            return await database.LeagueRules.SingleOrDefaultAsync(x => x.GroupId == team.GroupId && x.LeagueId == contextLeague && x.SeasonIndex == competitionYear, cancellationToken)
                ?? throw new ApplicationFault(409, "formation_rules_unavailable", "Regolamento non disponibile.");
        }
        var participations = database.LeagueBasketSeasons.Where(x => x.GroupId == team.GroupId && x.BasketId == team.BasketId && x.SeasonIndex == competitionYear);
        if (contextLeague is { } selected && !await participations.AnyAsync(x => x.LeagueId == selected, cancellationToken)) throw Denied();
        var main = await participations.Where(x => x.LeagueSeason.League.IsMain).Select(x => x.LeagueId).Distinct().ToArrayAsync(cancellationToken);
        var all = await participations.Select(x => x.LeagueId).Distinct().ToArrayAsync(cancellationToken);
        var selectedLeague = main.Length == 1 ? main[0] : all.Length == 1 ? all[0] : Guid.Empty;
        if (main.Length > 1) throw new ApplicationFault(409, "ambiguous_formation_rules", "Più leghe principali condividono la rosa: correggere la configurazione.");
        if (selectedLeague == Guid.Empty) throw new ApplicationFault(409, "ambiguous_formation_rules", "Configurare una lega principale per la rosa condivisa.");
        return await database.LeagueRules.SingleOrDefaultAsync(x => x.GroupId == team.GroupId && x.LeagueId == selectedLeague && x.SeasonIndex == team.SeasonIndex, cancellationToken)
            ?? throw new ApplicationFault(409, "formation_rules_unavailable", "Regolamento non disponibile.");
    }

    private async Task<TeamRosterDto> DtoAsync(RosterSnapshot row, CancellationToken cancellationToken)
    {
        var current = row.Kind == RosterKind.Season;
        var metadata = await database.TeamSeasons.AsNoTracking().Include(x => x.Owners).ThenInclude(x => x.Membership).ThenInclude(x => x.User)
            .SingleAsync(x => x.Id == row.TeamId, cancellationToken);
        current = current && !metadata.IsArchived && metadata.Owners.Any(x => x.IsPrimary);
        var owner = current ? metadata.Owners.Single(x => x.IsPrimary).Membership.User.OwnerReference
            : (await database.Users.SingleAsync(x => x.Id == row.OwnerUserId, cancellationToken)).OwnerReference;
        return new(row.TeamId, row.GroupId, row.BasketId, row.SeasonIndex, (int)row.Kind, row.Day, row.LeagueId,
            current ? metadata.Name : row.Name, owner,
            current ? metadata.Owners.Where(x => !x.IsPrimary).Select(x => x.Membership.User.OwnerReference).ToArray() : row.AdditionalOwners.Select(x => x.User.OwnerReference).ToArray(),
            row.Players.OrderBy(x => x.Ordinal).Select(x => new RosterPlayerDto(x.PlayerId, x.Ordinal, x.Player.Name,
                new(x.Club.Name, x.Club.Id), x.PlayerRole, x.IsActive, x.IsVisible, x.Price, x.Revenue, x.Status, x.Position)
                { SeasonIndex = row.SeasonIndex }).ToArray(),
            row.MoneyFromRank, row.OpeningCompetitionPrize, row.LastUpdateUtc?.ToString("O") ?? row.LegacyLastUpdateLocal?.ToString("s"), row.FormationChanges,
            row.FormationHistory.OrderBy(x => x.Ordinal).Select(x => new FormationChangeDto(x.DateUtc?.ToString("O") ?? x.LegacyDateLocal?.ToString("s"), x.Author,
                x.Players.OrderBy(player => player.Ordinal).Select(player => new FormationPlayerChangeDto(player.PlayerName, player.FromPosition, player.ToPosition)).ToArray())).ToArray(), row.Version,
            AssetLinks.Logo(assets.Value, metadata.LogoBlobName), MarketBalanceAdjustment: row.MarketBalanceAdjustment, RankCreditGranted: row.RankCreditGranted);
    }
    internal static RosterSnapshot Copy(RosterSnapshot source, RosterKind kind, int day, Guid? competition) => new()
    {
        GroupId = source.GroupId, BasketId = source.BasketId, SeasonIndex = source.SeasonIndex, TeamId = source.TeamId, TeamOriginYear = source.TeamOriginYear,
        Kind = kind, Day = day, CompetitionId = competition ?? Guid.Empty, LeagueId = competition, Name = source.Name, OwnerUserId = source.OwnerUserId,
        MoneyFromRank = source.MoneyFromRank, OpeningCompetitionPrize = source.OpeningCompetitionPrize,
        MarketBalanceAdjustment = source.MarketBalanceAdjustment,
        RankCreditGranted = source.RankCreditGranted,
        AdditionalOwners = source.AdditionalOwners.Select(x => new RosterOwner { UserId = x.UserId, User = x.User }).ToList(),
        Players = source.Players.Select(x => new RosterPlayer { Ordinal = x.Ordinal, PlayerId = x.PlayerId, Player = x.Player,
            ClubId = x.ClubId, Club = x.Club, PlayerRole = x.PlayerRole, IsActive = x.IsActive, IsVisible = x.IsVisible,
            Price = x.Price, Revenue = x.Revenue, Position = x.Position, Status = x.Status }).ToList()
    };
    private async Task AuthorizeAsync(ClaimsPrincipal caller, Guid group)
    {
        if (!(await authorization.AuthorizeAsync(caller, group, "workspace-member")).Succeeded) throw Denied();
    }
    private static ApplicationFault Denied() => new(403, "team_access_denied", "Accesso alla squadra non consentito.");
    private static ApplicationFault Invalid(string message) => new(400, "invalid_formation", message);
    private static ApplicationFault Changed() => new(409, "roster_changed", "La rosa è cambiata. Ricaricare prima di salvare.");
}

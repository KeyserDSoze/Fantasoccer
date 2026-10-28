using System.Security.Claims;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Fantasoccer.Application.Core.Teams;

namespace Fantasoccer.Application.Core.Groups;

public sealed class GameGroupApplicationService(FantasoccerDbContext database, IAuthorizationService authorization,
    GroupApplicationService memberships, IOptions<AssetLinkOptions> assets)
{
    public async Task<GameGroupDto> GetAsync(ClaimsPrincipal caller, Guid groupId, CancellationToken cancellationToken = default)
    {
        await AuthorizeAsync(caller, groupId, false);
        return await ProjectAsync(groupId, cancellationToken);
    }

    private async Task<GameGroupDto> ProjectAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var group = await database.Groups.AsNoTracking().SingleAsync(x => x.Id == groupId, cancellationToken);
        var users = await database.GroupMemberships.Where(x => x.GroupId == groupId)
            .OrderBy(x => x.User.DisplayName).Select(x => new GameGroupUserDto(x.User.DisplayName,
                x.User.Email ?? "user:" + x.UserId.ToString(), (int)x.Roles)).ToArrayAsync(cancellationToken);
        var baskets = await database.Baskets.AsNoTracking().Include(x => x.Seasons).Where(x => x.GroupId == groupId && !x.IsArchived)
            .OrderBy(x => x.Name).AsSplitQuery().ToArrayAsync(cancellationToken);
        var basketOrigins = await database.BasketDerivations.AsNoTracking().Where(x => x.GroupId == groupId).ToDictionaryAsync(x => x.BasketId, x => x.OriginBasketId, cancellationToken);
        var teams = await database.TeamSeasons.AsNoTracking().Include(x => x.Owners).ThenInclude(x => x.Membership).ThenInclude(x => x.User)
            .Where(x => x.GroupId == groupId && !x.IsArchived).AsSplitQuery().ToArrayAsync(cancellationToken);
        var teamOrigins = await database.TeamDerivations.AsNoTracking().Where(x => x.GroupId == groupId).ToDictionaryAsync(x => x.TeamId, x => x.OriginTeamId, cancellationToken);
        var allTeams = teamOrigins.Count == 0 ? teams.ToDictionary(x => x.Id) : (await database.TeamSeasons.AsNoTracking().Include(x => x.Owners).ThenInclude(x => x.Membership).ThenInclude(x => x.User)
            .Where(x => x.GroupId == groupId && teamOrigins.Values.Contains(x.Id)).AsSplitQuery().ToArrayAsync(cancellationToken)).Concat(teams).DistinctBy(x => x.Id).ToDictionary(x => x.Id);
        var leagues = await database.Leagues.AsNoTracking().Include(x => x.Seasons).ThenInclude(x => x.Baskets).Include(x => x.BasketLinks)
            .Where(x => x.GroupId == groupId && !x.IsArchived).OrderBy(x => x.Name).AsSplitQuery().ToArrayAsync(cancellationToken);
        var rules = await database.LeagueRules.AsNoTracking().Where(x => x.GroupId == groupId).AsSplitQuery().ToArrayAsync(cancellationToken);
        var sources = await database.LeagueDerivations.AsNoTracking().Where(x => x.GroupId == groupId).ToArrayAsync(cancellationToken);
        var participations = await database.LeagueParticipations.AsNoTracking().Where(x => x.GroupId == groupId)
            .Select(x => new { x.LeagueId, x.SeasonIndex, x.TeamId }).ToArrayAsync(cancellationToken);
        // Derived teams expose the owners of their origin (read-only).
        FantasyTeamSeason Owning(FantasyTeamSeason team) => teamOrigins.TryGetValue(team.Id, out var origin) && allTeams.TryGetValue(origin, out var source) ? source : team;
        return new(groupId, group.Name,
            leagues.Select(league => new GameLeagueDto(league.LeagueId, league.Name, league.IsMain, league.CompetitionType,
                league.Seasons.Where(x => !x.IsArchived).OrderBy(x => x.SeasonIndex).Select(year => new GameAnnualLeagueDto(year.SeasonIndex,
                    year.CompetitionType, LeagueRulesMapping.ToDto(rules.SingleOrDefault(x => x.LeagueId == league.LeagueId && x.SeasonIndex == year.SeasonIndex)
                        ?? throw new ApplicationFault(409, "league_settings_not_imported", "Importare i regolamenti prima di utilizzare la lega.")),
                    sources.Where(x => x.LeagueId == league.LeagueId && x.SeasonIndex == year.SeasonIndex).OrderBy(x => x.Ordinal).Select(x => new LeagueSourceDto(x.SourceLeagueId, x.QualifiedTeams, x.Stage)).ToArray(),
                    participations.Where(x => x.LeagueId == league.LeagueId && x.SeasonIndex == year.SeasonIndex).Select(x => x.TeamId).Order().ToArray())).ToArray(),
                league.BasketLinks.Select(x => x.BasketId).Order().ToArray(), (int)league.Mode)).ToArray(), users,
            baskets.Select(basket => new GameBasketDto(basket.BasketId, basket.Name,
                basket.Seasons.Where(x => !x.IsArchived).OrderBy(x => x.SeasonIndex).Select(year => new GameBasketYearDto(year.SeasonIndex,
                    teams.Where(team => team.BasketId == basket.BasketId && team.SeasonIndex == year.SeasonIndex).OrderBy(team => team.Name)
                        .Select(team => new GameAnnualTeamDto(team.Name, Owning(team).Owners.SingleOrDefault(x => x.IsPrimary)?.Membership.User.OwnerReference ?? "",
                            Owning(team).Owners.Where(x => !x.IsPrimary).Select(x => x.Membership.User.OwnerReference).Order().ToArray(), team.Id,
                            AssetLinks.Logo(assets.Value, team.LogoBlobName ?? Owning(team).LogoBlobName),
                            teamOrigins.TryGetValue(team.Id, out var origin) ? origin : null)).ToArray())).ToArray(),
                (int)basket.Kind, basketOrigins.TryGetValue(basket.BasketId, out var basketOrigin) ? basketOrigin : null)).ToArray(), group.Revision);
    }

    public async Task<GameGroupDto[]> ListAsync(ClaimsPrincipal caller, CancellationToken cancellationToken = default)
    {
        var summaries = await memberships.ListAsync(caller, cancellationToken);
        var result = new List<GameGroupDto>();
        foreach (var group in summaries) result.Add(await GetAsync(caller, group.GroupId, cancellationToken));
        return result.ToArray();
    }

    public async Task<GameGroupDto> UpdateAsync(ClaimsPrincipal caller, Guid groupId, GameGroupDto request, CancellationToken cancellationToken = default)
    {
        if (request.Id != groupId || request.Baskets is null || request.Leagues is null || request.Users is null ||
            request.Baskets.Select(x => x.Id).Distinct().Count() != request.Baskets.Length ||
            request.Leagues.Select(x => x.Id).Distinct().Count() != request.Leagues.Length ||
            request.Users.Select(x => Canonical(x.Email)).Distinct().Count() != request.Users.Length)
            throw Invalid("Identificatori duplicati o gruppo non valido.");
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        await AuthorizeAsync(caller, groupId, true);
        var group = await database.Groups.SingleAsync(x => x.Id == groupId, cancellationToken);
        if (request.Revision != group.Revision) throw new ApplicationFault(409, "group_changed", "Il gruppo è cambiato. Ricarica prima di salvare.");
        group.Name = Name(request.Name);
        var actorEmail = caller.FindFirstValue(ClaimTypes.Email)!.Trim().ToLowerInvariant();
        var actorId = await database.Users.Where(x => x.Email == actorEmail && x.IsEnabled).Select(x => x.Id).SingleAsync(cancellationToken);

        var oldMembers = await database.GroupMemberships.Where(x => x.GroupId == groupId).Include(x => x.User).ToArrayAsync(cancellationToken);
        var changes = new List<GameGroupUserDto>();
        foreach (var member in request.Users)
        {
            var previous = oldMembers.SingleOrDefault(x => x.User.OwnerReference == Canonical(member.Email));
            if (previous is null || (int)previous.Roles != member.Role)
                changes.Add(member);
        }
        foreach (var previous in oldMembers.Where(x => x.Roles != GroupRole.None && !request.Users.Any(member => Canonical(member.Email) == x.User.OwnerReference)))
            changes.Add(new(previous.User.DisplayName, previous.User.OwnerReference, 0));
        // Promote successors first and change the actor's own role last. Flush
        // inside the transaction so last-manager checks see the intermediate rows;
        // any later failure still rolls back the entire group command.
        foreach (var member in changes.OrderBy(x => Canonical(x.Email) == actorEmail ? 1 : 0)
                     .ThenByDescending(x => (x.Role & 8) != 0 ? 2 : (x.Role & 4) != 0 ? 1 : 0))
        {
            var previous = oldMembers.SingleOrDefault(x => x.User.OwnerReference == Canonical(member.Email));
            if (previous?.User.Email is null && previous is not null)
                await memberships.SetMemberRolesCoreAsync(caller, groupId, previous.User, member.Role, cancellationToken);
            else await memberships.SetMemberCoreAsync(caller, groupId, new(member.Email, member.Username, member.Role), cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
        }
        await database.SaveChangesAsync(cancellationToken);

        var allUsers = await database.GroupMemberships.Where(x => x.GroupId == groupId).Include(x => x.User).ToArrayAsync(cancellationToken);
        var baskets = await database.Baskets.Where(x => x.GroupId == groupId).ToArrayAsync(cancellationToken);
        var derivedBaskets = await database.BasketDerivations.AsNoTracking().Where(x => x.GroupId == groupId).ToDictionaryAsync(x => x.BasketId, x => (Guid?)x.OriginBasketId, cancellationToken);
        var derivedTeams = await database.TeamDerivations.AsNoTracking().Where(x => x.GroupId == groupId).ToDictionaryAsync(x => x.TeamId, x => x.OriginTeamId, cancellationToken);
        var basketYears = await database.BasketSeasons.Where(x => x.GroupId == groupId).ToListAsync(cancellationToken);
        foreach (var previous in baskets) previous.IsArchived = !request.Baskets.Any(x => x.Id == previous.BasketId);
        foreach (var previous in basketYears)
            previous.IsArchived = !request.Baskets.Any(basket => basket.Id == previous.BasketId && basket.Years.Any(x => x.Year == previous.SeasonIndex));
        var teams = await database.TeamSeasons.Include(x => x.Owners).ThenInclude(x => x.Membership).ThenInclude(x => x.User)
            .Where(x => x.GroupId == groupId).AsSplitQuery().ToArrayAsync(cancellationToken);
        var previousOwners = teams.ToDictionary(x => x.Id, x => x.Owners.Select(owner => (owner.UserId, owner.IsPrimary)).ToHashSet());
        var incomingTeams = new List<(FantasyTeamSeason Team, Guid Primary, Guid[] Owners)>();
        var renamedDerived = new List<FantasyTeamSeason>();
        var retainedTeams = new HashSet<Guid>();
        // Autonomous baskets first: derived baskets and teams reference them.
        foreach (var basketDto in request.Baskets.OrderBy(x => x.Kind))
        {
            if (basketDto.Id == Guid.Empty || basketDto.Years is null || basketDto.Years.Select(x => x.Year).Distinct().Count() != basketDto.Years.Length)
                throw Invalid("Basket non valido.");
            var basket = baskets.SingleOrDefault(x => x.BasketId == basketDto.Id);
            if (basketDto.Kind is not (0 or 1) || (basketDto.Kind == 1) != (basketDto.OriginBasketId is not null)) throw Invalid("Tipo di basket non valido.");
            if (basket is null)
            {
                if (basketDto.OriginBasketId is { } origin)
                {
                    var originDto = request.Baskets.SingleOrDefault(x => x.Id == origin);
                    if (originDto is null || originDto.Kind != 0 || !baskets.Any(x => x.BasketId == origin && x.Kind == BasketKind.Autonomous))
                        throw Invalid("Un basket derivato deriva da un basket autonomo già salvato nel gruppo.");
                    await database.SaveChangesAsync(cancellationToken);
                    basket = await Competitions.CompetitionStructure.CreateDerivedBasketAsync(database, groupId, basketDto.Id, Name(basketDto.Name),
                        basketDto.Years.FirstOrDefault()?.Year ?? throw Invalid("Specificare la stagione del basket derivato."), origin, cancellationToken);
                }
                else database.Baskets.Add(basket = new() { GroupId = groupId, BasketId = basketDto.Id, Name = Name(basketDto.Name) });
                baskets = [.. baskets, basket];
            }
            else if ((int)basket.Kind != basketDto.Kind || derivedBaskets.GetValueOrDefault(basket.BasketId) != basketDto.OriginBasketId)
                throw Invalid("Tipo e origine di un basket esistente non possono cambiare.");
            basket.Name = Name(basketDto.Name); basket.IsArchived = false;
            foreach (var annual in basketDto.Years)
            {
                await EnsureSeasonAsync(annual.Year, cancellationToken);
                var existingYear = basketYears.SingleOrDefault(x => x.BasketId == basketDto.Id && x.SeasonIndex == annual.Year);
                if (existingYear is null) { existingYear = new() { GroupId = groupId, BasketId = basketDto.Id, SeasonIndex = annual.Year }; database.BasketSeasons.Add(existingYear); basketYears.Add(existingYear); }
                existingYear.IsArchived = false;
                if (basket.Kind == BasketKind.Derived)
                {
                    // Derived teams: identity by TeamId or origin, never by owners
                    // (they have none); names stay their own competition labels.
                    if (annual.Teams.Any(x => x.TeamId is null && x.OriginTeamId is null) ||
                        annual.Teams.Select(x => Name(x.Name).ToLowerInvariant()).Distinct().Count() != annual.Teams.Length) throw Invalid("Squadre derivate duplicate o senza origine.");
                    foreach (var dto in annual.Teams)
                    {
                        var scoped = teams.Where(x => x.BasketId == basketDto.Id && x.SeasonIndex == annual.Year).ToArray();
                        var team = scoped.SingleOrDefault(x => dto.TeamId == x.Id) ?? scoped.SingleOrDefault(x => dto.OriginTeamId is { } o && derivedTeams.GetValueOrDefault(x.Id) == o);
                        if (team is null)
                        {
                            if (dto.OriginTeamId is not { } originTeam) throw Invalid("Squadra derivata sconosciuta.");
                            await database.SaveChangesAsync(cancellationToken);
                            team = await Competitions.CompetitionStructure.CreateDerivedTeamAsync(database, groupId, basketDto.Id, annual.Year, originTeam, dto.Name, DateTimeOffset.UtcNow, cancellationToken);
                            derivedTeams[team.Id] = originTeam;
                        }
                        else if (dto.OriginTeamId is { } declared && derivedTeams.GetValueOrDefault(team.Id) != declared) throw Invalid("L'origine di una squadra derivata non può cambiare.");
                        if (!retainedTeams.Add(team.Id)) throw Invalid("La stessa squadra è presente più volte.");
                        team.Name = Name(dto.Name); team.NormalizedName = "updating-" + team.Id.ToString("N"); team.IsArchived = false;
                        renamedDerived.Add(team);
                    }
                    continue;
                }
                if (annual.Teams.Any(x => x.OriginTeamId is not null)) throw Invalid("Le squadre di un basket autonomo non hanno una squadra di origine.");
                if (annual.Teams.Select(x => Canonical(x.Owner)).Distinct().Count() != annual.Teams.Length ||
                    annual.Teams.Select(x => Name(x.Name).ToLowerInvariant()).Distinct().Count() != annual.Teams.Length) throw Invalid("Squadre o proprietari duplicati.");
                foreach (var dto in annual.Teams)
                {
                    var owner = allUsers.SingleOrDefault(x => x.User.OwnerReference == Canonical(dto.Owner)) ?? throw Invalid("Proprietario non presente nel gruppo.");
                    var scoped = teams.Where(x => x.BasketId == basketDto.Id && x.SeasonIndex == annual.Year).ToArray();
                    var team = scoped.SingleOrDefault(x => dto.TeamId == x.Id) ?? scoped.SingleOrDefault(x => x.Owners.Any(o => o.IsPrimary && o.UserId == owner.UserId));
                    if (team is null)
                    {
                        if (owner.Roles == GroupRole.None || !owner.User.IsEnabled) throw Invalid("Il nuovo proprietario deve essere un membro attivo.");
                        team = new() { Id = Guid.NewGuid(), GroupId = groupId, BasketId = basketDto.Id, SeasonIndex = annual.Year, Name = Name(dto.Name), NormalizedName = Guid.NewGuid().ToString("N") };
                        database.TeamSeasons.Add(team);
                        database.RosterSnapshots.Add(new() { GroupId = groupId, BasketId = basketDto.Id, SeasonIndex = annual.Year, TeamId = team.Id,
                            TeamOriginYear = annual.Year, Kind = RosterKind.Season, Day = 0, CompetitionId = Guid.Empty, Name = Name(dto.Name), OwnerUserId = owner.UserId, RankCreditGranted = 0, LastUpdateUtc = DateTimeOffset.UtcNow });
                    }
                    if (!retainedTeams.Add(team.Id)) throw Invalid("La stessa squadra è presente più volte.");
                    team.Name = Name(dto.Name); team.NormalizedName = "updating-" + team.Id.ToString("N"); team.IsArchived = false;
                    var owners = (dto.AdditionalOwners ?? []).Select(Canonical).Append(owner.User.OwnerReference).Distinct()
                        .Select(email => allUsers.SingleOrDefault(x => x.User.OwnerReference == email)?.UserId ?? throw Invalid("Comproprietario non presente nel gruppo.")).ToArray();
                    incomingTeams.Add((team, owner.UserId, owners));
                }
            }
        }
        foreach (var old in teams.Where(x => !retainedTeams.Contains(x.Id))) old.IsArchived = true;
        // Remove current ownership links before applying swaps, while retaining a
        // normalized audit trail. Historical lineups keep the same stable team ID.
        database.TeamOwnerships.RemoveRange(teams.SelectMany(x => x.Owners));
        await database.SaveChangesAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        foreach (var team in teams.Concat(incomingTeams.Select(x => x.Team)).DistinctBy(x => x.Id))
        {
            var desired = incomingTeams.SingleOrDefault(x => x.Team.Id == team.Id);
            var after = desired.Team is null ? [] : desired.Owners.Select(id => (UserId: id, IsPrimary: id == desired.Primary)).ToHashSet();
            var before = previousOwners.GetValueOrDefault(team.Id) ?? [];
            foreach (var change in before.Except(after).Select(x => (Owner: x, Added: false)).Concat(after.Except(before).Select(x => (Owner: x, Added: true))))
                database.TeamOwnershipChanges.Add(new() { GroupId = groupId, BasketId = team.BasketId, SeasonIndex = team.SeasonIndex,
                    TeamId = team.Id, UserId = change.Owner.UserId, IsPrimary = change.Owner.IsPrimary, Added = change.Added, ActorUserId = actorId, ChangedAtUtc = now });
        }
        foreach (var derived in renamedDerived) derived.NormalizedName = derived.Name.ToLowerInvariant();
        foreach (var desired in incomingTeams)
        {
            desired.Team.NormalizedName = desired.Team.Name.ToLowerInvariant();
            foreach (var owner in desired.Owners) database.TeamOwnerships.Add(new() { GroupId = groupId, BasketId = desired.Team.BasketId,
                SeasonIndex = desired.Team.SeasonIndex, TeamId = desired.Team.Id, UserId = owner, IsPrimary = owner == desired.Primary });
        }

        var leagues = await database.Leagues.Where(x => x.GroupId == groupId).ToArrayAsync(cancellationToken);
        var leagueYears = await database.LeagueSeasons.Where(x => x.GroupId == groupId).ToListAsync(cancellationToken);
        var pendingSources = new List<(Guid League, int Year, LeagueSourceDto[] Sources)>();
        foreach (var previous in leagues) previous.IsArchived = !request.Leagues.Any(x => x.Id == previous.LeagueId);
        foreach (var previous in leagueYears) previous.IsArchived = !request.Leagues.Any(league => league.Id == previous.LeagueId && league.Years.Any(x => x.Year == previous.SeasonIndex));
        foreach (var dto in request.Leagues)
        {
            if (dto.Id == Guid.Empty || dto.Type is < 0 or > 5 || dto.Years.Select(x => x.Year).Distinct().Count() != dto.Years.Length ||
                dto.BasketsId.Any(id => !request.Baskets.Any(x => x.Id == id))) throw Invalid("Lega o partecipazioni non valide.");
            var league = leagues.SingleOrDefault(x => x.LeagueId == dto.Id);
            if (dto.Mode is not (0 or 1)) throw Invalid("Modalità della lega non valida.");
            if (league is null) database.Leagues.Add(league = new() { GroupId = groupId, LeagueId = dto.Id, Name = Name(dto.Name), Mode = (LeagueMode)dto.Mode });
            else if ((int)league.Mode != dto.Mode) throw Invalid("La modalità (autonoma/derivata) di una lega esistente non può cambiare.");
            await Competitions.CompetitionStructure.ValidateBasketKindsAsync(database, groupId, league.Mode, dto.BasketsId, cancellationToken);
            league.Name = Name(dto.Name); league.IsMain = dto.IsMain; league.CompetitionType = dto.Type; league.IsArchived = false;
            await database.GroupLeagueBaskets.Where(x => x.GroupId == groupId && x.LeagueId == dto.Id).ExecuteDeleteAsync(cancellationToken);
            foreach (var basket in dto.BasketsId.Distinct()) database.GroupLeagueBaskets.Add(new() { GroupId = groupId, LeagueId = dto.Id, BasketId = basket });
            foreach (var annual in dto.Years)
            {
                await EnsureSeasonAsync(annual.Year, cancellationToken);
                var year = leagueYears.SingleOrDefault(x => x.LeagueId == dto.Id && x.SeasonIndex == annual.Year);
                if (year is null) { year = new() { GroupId = groupId, LeagueId = dto.Id, SeasonIndex = annual.Year }; database.LeagueSeasons.Add(year); leagueYears.Add(year); }
                if (annual.Type is < 0 or > 5) throw Invalid("Tipo di competizione non valido.");
                year.CompetitionType = annual.Type; year.IsArchived = false;
                var rules = LeagueRulesMapping.FromDto(groupId, dto.Id, annual.Year, annual.Settings);
                if (rules.PreviousLeagues.Any(x => !request.Leagues.Any(leagueDto => leagueDto.Id == x.SourceLeagueId) && !leagues.Any(known => known.LeagueId == x.SourceLeagueId)))
                    throw Invalid("Lega di qualificazione esterna al gruppo.");
                await database.LeagueRules.Where(x => x.GroupId == groupId && x.LeagueId == dto.Id && x.SeasonIndex == annual.Year).ExecuteDeleteAsync(cancellationToken);
                database.LeagueRules.Add(rules);
                // Keep unchanged participation keys: saved standings/matches may
                // reference them even when this command only changes a member.
                var desiredBaskets = dto.BasketsId.Distinct().Where(id => basketYears.Any(x => x.BasketId == id && x.SeasonIndex == annual.Year && !x.IsArchived)).ToArray();
                var previousBaskets = await database.LeagueBasketSeasons.Where(x => x.GroupId == groupId && x.LeagueId == dto.Id && x.SeasonIndex == annual.Year)
                    .Select(x => x.BasketId).ToArrayAsync(cancellationToken);
                await database.LeagueBasketSeasons.Where(x => x.GroupId == groupId && x.LeagueId == dto.Id && x.SeasonIndex == annual.Year && !desiredBaskets.Contains(x.BasketId)).ExecuteDeleteAsync(cancellationToken);
                foreach (var basketId in desiredBaskets.Except(previousBaskets))
                    database.LeagueBasketSeasons.Add(new() { GroupId = groupId, LeagueId = dto.Id, BasketId = basketId, SeasonIndex = annual.Year });
                if (annual.Sources is { } sources)
                {
                    if (league.Mode == LeagueMode.Autonomous && sources.Length > 0) throw Invalid("Solo una lega derivata dichiara leghe di origine.");
                    pendingSources.Add((dto.Id, annual.Year, sources));
                }
            }
        }
        await database.SaveChangesAsync(cancellationToken);
        foreach (var (leagueId, year, sources) in pendingSources)
            await Competitions.CompetitionStructure.SetLeagueSourcesAsync(database, groupId, leagueId, year, sources, cancellationToken);
        // Explicit enrolment of autonomous leagues; owner uniqueness is checked by
        // the database for every resulting participation in this same transaction.
        await Competitions.CompetitionStructure.EnsureAutonomousEnrollmentAsync(database, groupId, actorId, now, cancellationToken);
        group.Revision++;
        await DatabaseFaults.GuardAsync(() => database.SaveChangesAsync(cancellationToken));
        var result = await ProjectAsync(groupId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private async Task EnsureSeasonAsync(int year, CancellationToken cancellationToken)
    {
        if (year is < 1 or > 200) throw Invalid("Indice stagione non valido.");
        if (!database.Seasons.Local.Any(x => x.SeasonIndex == year) && !await database.Seasons.AnyAsync(x => x.SeasonIndex == year, cancellationToken))
            database.Seasons.Add(new() { SeasonIndex = year, StartYear = year + 2011 });
    }
    private async Task AuthorizeAsync(ClaimsPrincipal caller, Guid groupId, bool manage)
    {
        if (!(await authorization.AuthorizeAsync(caller, groupId, manage ? "workspace-manager" : "workspace-member")).Succeeded)
            throw new ApplicationFault(403, "group_access_denied", "Accesso al gruppo non consentito.");
    }
    private static string Name(string? value) => value?.Trim() is { Length: >= 2 and <= 200 } name ? name : throw Invalid("Nome non valido.");
    private static string Canonical(string email) => email.Trim().ToLowerInvariant();
    private static ApplicationFault Invalid(string message) => new(400, "invalid_group", message);
}

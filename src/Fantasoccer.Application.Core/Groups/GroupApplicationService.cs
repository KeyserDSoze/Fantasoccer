using System.Net.Mail;
using System.Security.Claims;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Fantasoccer.Application.Core.Accounts;

namespace Fantasoccer.Application.Core.Groups;

public sealed class GroupApplicationService(FantasoccerDbContext database, IAuthorizationService authorization, IOptions<GlobalAdministratorOptions> administrators)
{
    public async Task<AccountSummaryDto> GetAccountAsync(ClaimsPrincipal caller, CancellationToken cancellationToken = default)
    {
        var user = await AccountAsync(caller, cancellationToken);
        return new(user.Id, user.Email, user.DisplayName, GlobalAdmin(caller, user.Email));
    }

    public async Task<GroupSummaryDto[]> ListAsync(ClaimsPrincipal caller, CancellationToken cancellationToken = default)
    {
        var user = await AccountAsync(caller, cancellationToken);
        return await database.GroupMemberships.Where(x => x.UserId == user.Id && x.Roles != GroupRole.None)
            .OrderBy(x => x.Group.Name).Select(x => new GroupSummaryDto(x.GroupId, x.Group.Name, (int)x.Roles))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<GroupDetailsDto> GetAsync(ClaimsPrincipal caller, Guid groupId, CancellationToken cancellationToken = default)
    {
        var user = await AccountAsync(caller, cancellationToken);
        await AuthorizeAsync(caller, groupId, manage: false);
        var group = await database.Groups.AsNoTracking().SingleAsync(x => x.Id == groupId, cancellationToken);
        var members = await database.GroupMemberships.Where(x => x.GroupId == groupId)
            .OrderBy(x => x.User.DisplayName)
            .Select(x => new GroupMemberDto(x.UserId, x.User.Email, x.User.DisplayName, (int)x.Roles)).ToArrayAsync(cancellationToken);
        var baskets = await database.Baskets.AsNoTracking().Include(x => x.Seasons)
            .Where(x => x.GroupId == groupId).OrderBy(x => x.Name).ToArrayAsync(cancellationToken);
        var leagues = await database.Leagues.AsNoTracking().Include(x => x.Seasons).ThenInclude(x => x.Baskets)
            .Where(x => x.GroupId == groupId).OrderBy(x => x.Name).AsSplitQuery().ToArrayAsync(cancellationToken);
        var teams = await database.TeamSeasons.AsNoTracking().Include(x => x.Owners).ThenInclude(x => x.Membership).ThenInclude(x => x.User)
            .Where(x => x.GroupId == groupId).OrderBy(x => x.SeasonIndex).ThenBy(x => x.Name).AsSplitQuery().ToArrayAsync(cancellationToken);
        return new(groupId, group.Name, members.Single(x => x.UserId == user.Id).Roles, members,
            baskets.Select(x => new BasketDto(x.BasketId, x.Name, x.Seasons.Select(s => s.SeasonIndex).Order().ToArray())).ToArray(),
            leagues.Select(x => new LeagueDto(x.LeagueId, x.Name, x.IsMain,
                x.Seasons.OrderBy(s => s.SeasonIndex).Select(s => new LeagueSeasonDto(s.SeasonIndex, s.Baskets.Select(b => b.BasketId).Order().ToArray())).ToArray())).ToArray(),
            teams.Select(x => new TeamSummaryDto(x.Id, x.BasketId, x.SeasonIndex, x.Name,
                x.Owners.OrderByDescending(o => o.IsPrimary).ThenBy(o => o.UserId)
                    .Select(o => new TeamOwnerDto(o.UserId, o.Membership.User.DisplayName, o.IsPrimary)).ToArray())).ToArray());
    }

    public async Task<GroupSummaryDto> CreateAsync(ClaimsPrincipal caller, CreateGroupRequest request, CancellationToken cancellationToken = default)
    {
        var user = await AccountAsync(caller, cancellationToken);
        if (!GlobalAdmin(caller, user.Email)) throw Forbidden();
        var name = Name(request.Name);
        await using var transaction = await BeginWriteAsync(cancellationToken);
        var group = new FantasyGroup { Name = name };
        database.GroupMemberships.Add(new() { Group = group, UserId = user.Id, Roles = GroupRole.SuperAdmin | GroupRole.Admin });
        await CommitAsync(transaction, cancellationToken);
        return new(group.Id, group.Name, (int)(GroupRole.SuperAdmin | GroupRole.Admin));
    }

    public async Task RenameAsync(ClaimsPrincipal caller, Guid groupId, RenameGroupRequest request, CancellationToken cancellationToken = default)
    {
        var name = Name(request.Name);
        await using var transaction = await BeginWriteAsync(cancellationToken);
        await AuthorizeAsync(caller, groupId, manage: true);
        var group = await database.Groups.SingleAsync(x => x.Id == groupId, cancellationToken);
        group.Name = name;
        group.Revision++;
        await CommitAsync(transaction, cancellationToken);
    }

    public async Task SetMemberAsync(ClaimsPrincipal caller, Guid groupId, SetGroupMemberRequest request, CancellationToken cancellationToken = default)
        => await SetMembersAsync(caller, [(groupId, request)], cancellationToken);

    public async Task SetMembersAsync(ClaimsPrincipal caller, IReadOnlyList<(Guid GroupId, SetGroupMemberRequest Request)> changes, CancellationToken cancellationToken = default)
    {
        await using var transaction = await BeginWriteAsync(cancellationToken);
        foreach (var change in changes) await SetMemberCoreAsync(caller, change.GroupId, change.Request, cancellationToken);
        await CommitAsync(transaction, cancellationToken);
    }

    internal async Task SetMemberCoreAsync(ClaimsPrincipal caller, Guid groupId, SetGroupMemberRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim().ToLowerInvariant();
        if (email is null || email.Length > 320 || !MailAddress.TryCreate(email, out var address) || address.Address != email ||
            request.Roles is < 0 or > 15) throw Invalid("Email o ruoli non validi.");
        var name = Name(request.DisplayName);
        await AuthorizeAsync(caller, groupId, manage: true);
        var actor = await AccountAsync(caller, cancellationToken);
        var actorRoles = await database.GroupMemberships.Where(x => x.GroupId == groupId && x.UserId == actor.Id)
            .Select(x => x.Roles).SingleAsync(cancellationToken);
        var target = database.Users.Local.SingleOrDefault(x => x.Email == email)
            ?? await database.Users.SingleOrDefaultAsync(x => x.Email == email, cancellationToken);
        var previous = target is null ? null : await database.GroupMemberships.SingleOrDefaultAsync(
            x => x.GroupId == groupId && x.UserId == target.Id, cancellationToken);
        if (!actorRoles.HasFlag(GroupRole.SuperAdmin) &&
            (((GroupRole)request.Roles).HasFlag(GroupRole.SuperAdmin) || previous?.Roles.HasFlag(GroupRole.SuperAdmin) == true))
            throw Forbidden();
        if (previous?.Roles.HasFlag(GroupRole.SuperAdmin) == true && !((GroupRole)request.Roles).HasFlag(GroupRole.SuperAdmin) &&
            await database.GroupMemberships.CountAsync(x => x.GroupId == groupId && x.User.IsEnabled && (x.Roles & GroupRole.SuperAdmin) != 0, cancellationToken) <= 1)
            throw new ApplicationFault(409, "last_group_owner", "Non puoi rimuovere l'ultimo responsabile del gruppo.");
        const GroupRole managers = GroupRole.Admin | GroupRole.SuperAdmin;
        if (previous is not null && (previous.Roles & managers) != 0 && ((GroupRole)request.Roles & managers) == 0 &&
            await database.GroupMemberships.CountAsync(x => x.GroupId == groupId && x.User.IsEnabled && (x.Roles & managers) != 0, cancellationToken) <= 1)
            throw new ApplicationFault(409, "last_group_manager", "Non puoi rimuovere l'ultimo amministratore del gruppo.");
        if (target is null)
        {
            target = new() { Email = email, DisplayName = name };
            database.Users.Add(target);
        }
        else if (target.IsHistoricalPlaceholder && request.Roles != 0)
        {
            target.IsHistoricalPlaceholder = false; target.IsEnabled = true; target.DisplayName = name;
        }
        else if (!target.IsEnabled && request.Roles != 0)
            throw new ApplicationFault(409, "account_disabled", "L'account è disabilitato: serve l'intervento dell'amministratore globale.");
        // An invitation never edits another user's global profile.
        if (previous is null) database.GroupMemberships.Add(new() { GroupId = groupId, User = target, Roles = (GroupRole)request.Roles });
        else previous.Roles = (GroupRole)request.Roles;
        (await database.Groups.SingleAsync(x => x.Id == groupId, cancellationToken)).Revision++;
    }

    public async Task<Guid> CreateBasketAsync(ClaimsPrincipal caller, Guid groupId, CreateBasketRequest request, CancellationToken cancellationToken = default)
    {
        var name = Name(request.Name);
        await using var transaction = await BeginWriteAsync(cancellationToken);
        await AuthorizeAsync(caller, groupId, manage: true);
        await SeasonAsync(request.SeasonIndex, cancellationToken);
        var basket = new GroupBasket { GroupId = groupId, BasketId = Guid.NewGuid(), Name = name };
        database.BasketSeasons.Add(new() { Basket = basket, SeasonIndex = request.SeasonIndex });
        (await database.Groups.SingleAsync(x => x.Id == groupId, cancellationToken)).Revision++;
        await CommitAsync(transaction, cancellationToken);
        return basket.BasketId;
    }

    public async Task<Guid> CreateLeagueAsync(ClaimsPrincipal caller, Guid groupId, CreateLeagueRequest request, CancellationToken cancellationToken = default)
    {
        var name = Name(request.Name);
        var basketIds = request.BasketIds?.Distinct().ToArray() ?? [];
        if (basketIds.Length == 0 || basketIds.Length > 50) throw Invalid("Selezionare almeno un basket.");
        await using var transaction = await BeginWriteAsync(cancellationToken);
        await AuthorizeAsync(caller, groupId, manage: true);
        if (await database.BasketSeasons.CountAsync(x => x.GroupId == groupId && x.SeasonIndex == request.SeasonIndex &&
                basketIds.Contains(x.BasketId), cancellationToken) != basketIds.Length)
            throw Invalid("Basket non appartenente al gruppo o alla stagione.");
        var league = new GroupLeague { GroupId = groupId, LeagueId = Guid.NewGuid(), Name = name, CompetitionType = 1 };
        var season = new LeagueSeason { League = league, SeasonIndex = request.SeasonIndex, CompetitionType = 1,
            Rules = LeagueRulesMapping.Default(groupId, league.LeagueId, request.SeasonIndex) };
        foreach (var basket in basketIds) season.Baskets.Add(new() { GroupId = groupId, BasketId = basket, SeasonIndex = request.SeasonIndex });
        foreach (var basket in basketIds) league.BasketLinks.Add(new() { GroupId = groupId, BasketId = basket, LeagueId = league.LeagueId });
        database.LeagueSeasons.Add(season);
        (await database.Groups.SingleAsync(x => x.Id == groupId, cancellationToken)).Revision++;
        await CommitAsync(transaction, cancellationToken);
        return league.LeagueId;
    }

    public async Task<Guid> CreateTeamAsync(ClaimsPrincipal caller, Guid groupId, CreateTeamRequest request, CancellationToken cancellationToken = default)
    {
        var name = Name(request.Name);
        var owners = (request.AdditionalOwnerUserIds ?? []).Append(request.OwnerUserId).Distinct().ToArray();
        if (owners.Length > 20) throw Invalid("Troppi comproprietari.");
        await using var transaction = await BeginWriteAsync(cancellationToken);
        await AuthorizeAsync(caller, groupId, manage: true);
        if (!await database.BasketSeasons.AnyAsync(x => x.GroupId == groupId && x.BasketId == request.BasketId && x.SeasonIndex == request.SeasonIndex, cancellationToken) ||
            await database.GroupMemberships.CountAsync(x => x.GroupId == groupId && owners.Contains(x.UserId) && x.Roles != GroupRole.None && x.User.IsEnabled, cancellationToken) != owners.Length)
            throw Invalid("Basket o proprietari non validi per il gruppo.");
        if (await database.TeamSeasons.AnyAsync(x => x.GroupId == groupId && x.BasketId == request.BasketId && x.SeasonIndex == request.SeasonIndex && x.NormalizedName == name.ToLowerInvariant(), cancellationToken))
            throw new ApplicationFault(409, "duplicate_team_name", "Nome squadra già presente nel basket.");
        if (await database.TeamOwnerships.AnyAsync(x => x.GroupId == groupId && x.BasketId == request.BasketId &&
                x.SeasonIndex == request.SeasonIndex && x.UserId == request.OwnerUserId && x.IsPrimary, cancellationToken))
            throw new ApplicationFault(409, "duplicate_team_owner", "Il proprietario ha già una squadra in questo basket e stagione.");
        var team = new FantasyTeamSeason { GroupId = groupId, BasketId = request.BasketId, SeasonIndex = request.SeasonIndex, Name = name, NormalizedName = name.ToLowerInvariant() };
        foreach (var owner in owners) team.Owners.Add(new() { GroupId = groupId, UserId = owner, IsPrimary = owner == request.OwnerUserId });
        database.TeamSeasons.Add(team);
        database.RosterSnapshots.Add(new() { GroupId = groupId, BasketId = request.BasketId, SeasonIndex = request.SeasonIndex,
            TeamId = team.Id, TeamOriginYear = request.SeasonIndex, Kind = RosterKind.Season, Day = 0, CompetitionId = Guid.Empty, Name = name, OwnerUserId = request.OwnerUserId, RankCreditGranted = 0,
            LastUpdateUtc = DateTimeOffset.UtcNow, AdditionalOwners = owners.Where(id => id != request.OwnerUserId).Select(id => new RosterOwner { UserId = id }).ToList() });
        (await database.Groups.SingleAsync(x => x.Id == groupId, cancellationToken)).Revision++;
        await CommitAsync(transaction, cancellationToken);
        return team.Id;
    }

    private async Task<UserAccount> AccountAsync(ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (caller.Identity?.IsAuthenticated != true) throw new ApplicationFault(401, "authentication_required", "Accesso richiesto.");
        var email = caller.FindFirstValue(ClaimTypes.Email)?.Trim().ToLowerInvariant();
        return await database.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Email == email && x.IsEnabled, cancellationToken)
            ?? throw new ApplicationFault(403, "account_unavailable", "Account non disponibile o disabilitato.");
    }

    private async Task AuthorizeAsync(ClaimsPrincipal caller, Guid groupId, bool manage)
    {
        if (!(await authorization.AuthorizeAsync(caller, groupId, manage ? "workspace-manager" : "workspace-member")).Succeeded)
            throw Forbidden();
    }

    private Task<IDbContextTransaction> BeginWriteAsync(CancellationToken cancellationToken)
        => CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);

    private async Task CommitAsync(IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task SeasonAsync(int index, CancellationToken cancellationToken)
    {
        if (index is < 1 or > 200) throw Invalid("Indice stagione non valido.");
        if (!await database.Seasons.AnyAsync(x => x.SeasonIndex == index, cancellationToken))
            database.Seasons.Add(new() { SeasonIndex = index, StartYear = index + 2011 });
    }

    private static string Name(string? name) => name?.Trim() is { Length: >= 2 and <= 200 } value
        ? value : throw Invalid("Il nome deve contenere fra 2 e 200 caratteri.");
    private static ApplicationFault Forbidden() => new(403, "group_access_denied", "Operazione non consentita per questo gruppo.");
    private bool GlobalAdmin(ClaimsPrincipal caller, string email) => caller.IsInRole("super_admin") && administrators.Value.Emails.Any(value => string.Equals(value.Trim(), email, StringComparison.OrdinalIgnoreCase));
    private static ApplicationFault Invalid(string message) => new(400, "invalid_request", message);
}

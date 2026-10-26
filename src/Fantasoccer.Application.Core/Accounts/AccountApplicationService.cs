using System.Security.Claims;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fantasoccer.Application.Core.Accounts;

public sealed class AccountApplicationService(FantasoccerDbContext database, GroupApplicationService groups,
    IOptions<GlobalAdministratorOptions> administrators)
{
    public bool IsGlobalAdministrator(string email) => administrators.Value.Emails.Any(value =>
        string.Equals(value.Trim(), email, StringComparison.OrdinalIgnoreCase));

    public async Task<AccountIdentityDto?> FindForLoginAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var canonical = email.Trim().ToLowerInvariant();
        var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Email == canonical && x.IsEnabled, cancellationToken);
        return user is null ? null : await ProfileAsync(user, cancellationToken);
    }

    // Called only with the Microsoft claims already verified by the authentication
    // adapter. Never expose this as an email/subject selected by an HTTP client.
    public async Task<AccountIdentityDto?> ResolveExternalLoginAsync(string email, string issuer, string audience, string subject,
        CancellationToken cancellationToken = default)
    {
        if (subject.Length is < 1 or > 256 || issuer.Length > 256 || audience.Length is < 1 or > 128) return null;
        var existing = await database.ExternalLogins.AsNoTracking().Include(x => x.User)
            .SingleOrDefaultAsync(x => x.Issuer == issuer && x.Audience == audience && x.Subject == subject, cancellationToken);
        if (existing is not null) return existing.User.IsEnabled ? await ProfileAsync(existing.User, cancellationToken) : null;
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        existing = await database.ExternalLogins.AsNoTracking().Include(x => x.User)
            .SingleOrDefaultAsync(x => x.Issuer == issuer && x.Audience == audience && x.Subject == subject, cancellationToken);
        if (existing is not null) return existing.User.IsEnabled ? await ProfileAsync(existing.User, cancellationToken) : null;
        if (string.IsNullOrWhiteSpace(email)) return null;
        var canonical = email.Trim().ToLowerInvariant();
        var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Email == canonical && x.IsEnabled, cancellationToken);
        if (user is null || await database.ExternalLogins.AnyAsync(x => x.UserId == user.Id && x.Issuer == issuer && x.Audience == audience, cancellationToken)) return null;
        database.ExternalLogins.Add(new() { Issuer = issuer, Audience = audience, Subject = subject, UserId = user.Id });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ProfileAsync(user, cancellationToken);
    }

    public async Task<AccountIdentityDto> CurrentAsync(ClaimsPrincipal caller, CancellationToken cancellationToken = default)
        => await ProfileAsync(await CurrentUserAsync(caller, cancellationToken), cancellationToken);

    public async Task<AccountIdentityDto[]> SearchAsync(ClaimsPrincipal caller, string? email, CancellationToken cancellationToken = default)
    {
        var actor = await CurrentUserAsync(caller, cancellationToken);
        var global = IsGlobalAdministrator(actor.Email);
        var managed = await database.GroupMemberships.Where(x => x.UserId == actor.Id && (x.Roles & (GroupRole.Admin | GroupRole.SuperAdmin)) != 0)
            .Select(x => x.GroupId).ToArrayAsync(cancellationToken);
        if (!global && managed.Length == 0) throw Denied();
        var query = database.Users.AsNoTracking().Where(x => x.IsEnabled);
        if (!string.IsNullOrWhiteSpace(email))
        {
            var canonical = email.Trim().ToLowerInvariant();
            query = query.Where(x => x.Email == canonical);
        }
        else if (!global)
            query = query.Where(x => x.Memberships.Any(m => managed.Contains(m.GroupId)));
        var users = await query.OrderBy(x => x.Email).Take(200).ToArrayAsync(cancellationToken);
        var results = new List<AccountIdentityDto>();
        foreach (var user in users)
        {
            var profile = await ProfileAsync(user, cancellationToken);
            if (user.Id != actor.Id && !global)
                profile = profile with { State = new(), Groups = profile.Groups.Where(group => managed.Contains(group.Id)).ToArray() };
            results.Add(profile);
        }
        return results.ToArray();
    }

    public async Task UpdateProfileAsync(ClaimsPrincipal caller, UpdateAccountProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await CurrentUserAsync(caller, cancellationToken);
        var name = request.Username?.Trim();
        if (name is not { Length: >= 3 and <= 200 }) throw Invalid("Il nome deve contenere da 3 a 200 caratteri.");
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var stored = await database.Users.SingleAsync(x => x.Id == user.Id, cancellationToken);
        stored.DisplayName = name;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdateSelectionAsync(ClaimsPrincipal caller, AccountStateDto request, CancellationToken cancellationToken = default)
    {
        var user = await CurrentUserAsync(caller, cancellationToken);
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        if (request.GroupId is { } groupId && !await database.GroupMemberships.AnyAsync(x => x.GroupId == groupId && x.UserId == user.Id && x.Roles != GroupRole.None, cancellationToken))
            throw Denied();
        if (request.LeagueId is { } leagueId && (request.GroupId is null || !await database.Leagues.AnyAsync(x => x.GroupId == request.GroupId && x.LeagueId == leagueId, cancellationToken)))
            throw Invalid("La lega non appartiene al gruppo selezionato.");
        if (request.Year is { } year)
        {
            var configured = request.LeagueId is { } selectedLeague
                ? await database.LeagueSeasons.AnyAsync(x => x.GroupId == request.GroupId && x.LeagueId == selectedLeague && x.SeasonIndex == year, cancellationToken)
                : await database.BasketSeasons.AnyAsync(x => x.GroupId == request.GroupId && x.SeasonIndex == year, cancellationToken);
            if (!configured) throw Invalid("Stagione non configurata per la selezione.");
        }
        Guid? workingId = null;
        if (!string.IsNullOrWhiteSpace(request.MeOrLeaderEmail))
        {
            var workingEmail = request.MeOrLeaderEmail.Trim().ToLowerInvariant();
            workingId = await database.GroupMemberships.Where(x => x.GroupId == request.GroupId && x.User.Email == workingEmail)
                .Select(x => (Guid?)x.UserId).SingleOrDefaultAsync(cancellationToken);
            if (workingId is null) throw Invalid("Il proprietario selezionato non appartiene al gruppo.");
        }
        var selection = await database.AccountSelections.SingleOrDefaultAsync(x => x.UserId == user.Id, cancellationToken);
        if (selection is null) database.AccountSelections.Add(selection = new() { UserId = user.Id });
        selection.GroupId = request.GroupId; selection.LeagueId = request.LeagueId;
        selection.Year = request.Year; selection.WorkingUserId = workingId;
        await database.UnresolvedAccountSelections.Where(x => x.UserId == user.Id).ExecuteDeleteAsync(cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<UserSettingsDto> SettingsAsync(ClaimsPrincipal caller, CancellationToken cancellationToken = default)
    {
        var user = await CurrentUserAsync(caller, cancellationToken);
        return await database.AccountPreferences.Where(x => x.UserId == user.Id)
            .Select(x => new UserSettingsDto(x.AiPrompt, x.AutoOptimalFormation, x.LiveWidgetEnabled)).SingleOrDefaultAsync(cancellationToken)
            ?? new(null, false, true);
    }

    public async Task UpdateSettingsAsync(ClaimsPrincipal caller, UpdateUserSettingsRequest request, CancellationToken cancellationToken = default)
    {
        var user = await CurrentUserAsync(caller, cancellationToken);
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var settings = await database.AccountPreferences.SingleOrDefaultAsync(x => x.UserId == user.Id, cancellationToken);
        if (settings is null) database.AccountPreferences.Add(settings = new() { UserId = user.Id });
        settings.AutoOptimalFormation = request.AutoOptimalFormation;
        settings.LiveWidgetEnabled = request.LiveWidgetEnabled;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ProvisionAsync(ClaimsPrincipal caller, ProvisionAccountRequest request, CancellationToken cancellationToken = default)
    {
        await CurrentUserAsync(caller, cancellationToken);
        if (request.Groups is not { Length: > 0 and <= 50 } || request.Groups.Select(x => x.Id).Distinct().Count() != request.Groups.Length)
            throw Invalid("Specificare i gruppi amministrati per il nuovo account.");
        // Resource authorization happens in the same shared membership service as
        // controller/MCP administration; this never grants a global role.
        await groups.SetMembersAsync(caller, request.Groups.Select(group =>
            (group.Id, new SetGroupMemberRequest(request.Email, request.Username, group.Role))).ToArray(), cancellationToken);
    }

    public async Task DisableAsync(ClaimsPrincipal caller, string email, CancellationToken cancellationToken = default)
    {
        var actor = await CurrentUserAsync(caller, cancellationToken);
        if (!IsGlobalAdministrator(actor.Email)) throw Denied();
        var canonical = email.Trim().ToLowerInvariant();
        if (canonical == actor.Email || IsGlobalAdministrator(canonical)) throw Invalid("Non puoi disabilitare un amministratore globale con questa operazione.");
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var user = await database.Users.SingleOrDefaultAsync(x => x.Email == canonical, cancellationToken);
        if (user is not null)
        {
            var managed = await database.GroupMemberships.Where(x => x.UserId == user.Id && (x.Roles & (GroupRole.Admin | GroupRole.SuperAdmin)) != 0).ToArrayAsync(cancellationToken);
            foreach (var membership in managed)
            {
                var required = membership.Roles.HasFlag(GroupRole.SuperAdmin) ? GroupRole.SuperAdmin : GroupRole.Admin | GroupRole.SuperAdmin;
                if (!await database.GroupMemberships.AnyAsync(x => x.GroupId == membership.GroupId && x.UserId != user.Id && x.User.IsEnabled && (x.Roles & required) != 0, cancellationToken))
                    throw new ApplicationFault(409, "transfer_group_administration_first", "Assegnare prima un altro responsabile/amministratore attivo al gruppo.");
            }
            user.IsEnabled = false;
        }
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<AccountIdentityDto> ProfileAsync(UserAccount user, CancellationToken cancellationToken)
    {
        var memberships = await database.GroupMemberships.Where(x => x.UserId == user.Id && x.Roles != GroupRole.None)
            .OrderBy(x => x.Group.Name).Select(x => new IdentityGroupDto(x.GroupId, x.Group.Name, (int)x.Roles)).ToArrayAsync(cancellationToken);
        var selection = await database.AccountSelections.AsNoTracking().Include(x => x.WorkingUser).SingleOrDefaultAsync(x => x.UserId == user.Id, cancellationToken);
        return new(user.Id, user.DisplayName, user.Email,
            selection is null ? new() : new(selection.GroupId, selection.LeagueId, selection.Year, selection.WorkingUser?.Email), memberships);
    }

    private async Task<UserAccount> CurrentUserAsync(ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (caller.Identity?.IsAuthenticated != true) throw new ApplicationFault(401, "authentication_required", "Accesso richiesto.");
        var email = caller.FindFirstValue(ClaimTypes.Email)?.Trim().ToLowerInvariant();
        return await database.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Email == email && x.IsEnabled, cancellationToken) ?? throw Denied();
    }
    private static ApplicationFault Denied() => new(403, "account_access_denied", "Account non disponibile o accesso non consentito.");
    private static ApplicationFault Invalid(string message) => new(400, "invalid_account_request", message);
}

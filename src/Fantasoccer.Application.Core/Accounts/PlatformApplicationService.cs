using System.Security.Claims;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Infrastructure.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Application.Core.Accounts;

public sealed record PlatformPage<T>(T[] Items, int Total, int Offset, int Limit);
public sealed record PlatformUserDto(Guid Id, string? Email, string DisplayName, bool IsEnabled, bool IsGlobalAdministrator, IdentityGroupDto[] Groups);
public sealed record PlatformGroupDto(Guid Id, string Name, int Revision, int Members, int Leagues);
public sealed record PlatformGroupDetailsDto(Guid Id, string Name, GroupMemberDto[] Members, PlatformLeagueDto[] Leagues);
public sealed record PlatformLeagueDto(Guid Id, string Name, bool IsArchived);

// Platform oversight is explicit and read-only. It does not manufacture group
// memberships or bypass the authorization of gameplay/write services.
public sealed class PlatformApplicationService(FantasoccerDbContext database, AccountApplicationService accounts)
{
    public async Task<PlatformPage<PlatformUserDto>> UsersAsync(ClaimsPrincipal caller, string? query, int offset, int limit, CancellationToken cancellationToken = default)
    {
        await accounts.RequireGlobalAdministratorAsync(caller, cancellationToken);
        var search = Search(query); Page(offset, ref limit);
        var users = database.Users.AsNoTracking().AsQueryable();
        if (search.Length > 0) users = users.Where(x => (x.Email != null && x.Email.Contains(search)) || x.DisplayName.ToLower().Contains(search));
        var total = await users.CountAsync(cancellationToken);
        var rows = await users.OrderBy(x => x.Email).ThenBy(x => x.Id).Skip(offset).Take(limit)
            .Include(x => x.Memberships).ThenInclude(x => x.Group).AsSplitQuery().ToArrayAsync(cancellationToken);
        return new(rows.Select(x => new PlatformUserDto(x.Id, x.Email, x.DisplayName, x.IsEnabled, accounts.IsGlobalAdministrator(x.Email),
            x.Memberships.OrderBy(m => m.Group.Name).Select(m => new IdentityGroupDto(m.GroupId, m.Group.Name, (int)m.Roles)).ToArray())).ToArray(), total, offset, limit);
    }

    public async Task<PlatformPage<PlatformGroupDto>> GroupsAsync(ClaimsPrincipal caller, string? query, int offset, int limit, CancellationToken cancellationToken = default)
    {
        await accounts.RequireGlobalAdministratorAsync(caller, cancellationToken);
        var search = Search(query); Page(offset, ref limit);
        var groups = database.Groups.AsNoTracking().AsQueryable();
        if (search.Length > 0) groups = groups.Where(x => x.Name.ToLower().Contains(search));
        var total = await groups.CountAsync(cancellationToken);
        var rows = await groups.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip(offset).Take(limit)
            .Select(x => new PlatformGroupDto(x.Id, x.Name, x.Revision, x.Memberships.Count(m => m.Roles != 0), x.Leagues.Count)).ToArrayAsync(cancellationToken);
        return new(rows, total, offset, limit);
    }

    public async Task<PlatformGroupDetailsDto> GroupAsync(ClaimsPrincipal caller, Guid id, CancellationToken cancellationToken = default)
    {
        await accounts.RequireGlobalAdministratorAsync(caller, cancellationToken);
        var group = await database.Groups.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ApplicationFault(404, "group_not_found", "Gruppo non trovato.");
        var members = await database.GroupMemberships.AsNoTracking().Where(x => x.GroupId == id)
            .OrderBy(x => x.User.DisplayName).Select(x => new GroupMemberDto(x.UserId, x.User.Email, x.User.DisplayName, (int)x.Roles)).ToArrayAsync(cancellationToken);
        var leagues = await database.Leagues.AsNoTracking().Where(x => x.GroupId == id).OrderBy(x => x.Name)
            .Select(x => new PlatformLeagueDto(x.LeagueId, x.Name, x.IsArchived)).ToArrayAsync(cancellationToken);
        return new(group.Id, group.Name, members, leagues);
    }

    public async Task<PlatformPage<GroupCodeDto>> CodesAsync(ClaimsPrincipal caller, int offset, int limit, CancellationToken cancellationToken = default)
    {
        await accounts.RequireGlobalAdministratorAsync(caller, cancellationToken);
        Page(offset, ref limit);
        var total = await database.GroupCreationCodes.CountAsync(cancellationToken);
        var rows = await database.GroupCreationCodes.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            .Skip(offset).Take(limit).ToArrayAsync(cancellationToken);
        return new(rows.Select(GroupCreationApplicationService.Project).ToArray(), total, offset, limit);
    }

    private static string Search(string? query) => query?.Trim().ToLowerInvariant() is { Length: > 200 }
        ? throw new ApplicationFault(400, "invalid_search", "La ricerca è troppo lunga.") : query?.Trim().ToLowerInvariant() ?? "";
    private static void Page(int offset, ref int limit)
    {
        if (offset < 0 || limit < 1) throw new ApplicationFault(400, "invalid_page", "Pagina non valida.");
        limit = Math.Min(limit, 100);
    }
}

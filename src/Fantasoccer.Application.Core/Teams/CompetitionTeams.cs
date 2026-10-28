using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Application.Core.Teams;

public sealed record ResolvedOwner(Guid UserId, string? Email, string OwnerReference, string DisplayName, bool IsPrimary, bool IsActive);

// A competition participant resolved to the team that owns roster and economy.
// For an autonomous team both identities coincide; a derived team keeps its own
// identity (name, logo, lineups, results) but reads roster and owners from the origin.
public sealed record ResolvedTeam(Guid GroupId, int SeasonIndex, Guid TeamId, Guid BasketId, string Name, string? LogoBlobName, bool IsArchived,
    Guid AuthoritativeTeamId, Guid AuthoritativeBasketId, string? AuthoritativeLogoBlobName, ResolvedOwner[] Owners)
{
    public bool IsDerived => TeamId != AuthoritativeTeamId;
    public ResolvedOwner? Primary => Owners.FirstOrDefault(x => x.IsPrimary);
    public string OwnerReference => Primary?.OwnerReference ?? "";
    public string[] AdditionalOwners => Owners.Where(x => !x.IsPrimary).Select(x => x.OwnerReference).Order().ToArray();
    public bool IsControlledBy(Guid userId) => Owners.Any(x => x.UserId == userId && x.IsActive);
    public bool IsControlledBy(string email) => Owners.Any(x => x.IsActive && string.Equals(x.Email, email, StringComparison.OrdinalIgnoreCase));
    public string? Logo => LogoBlobName ?? AuthoritativeLogoBlobName;
}

public sealed class CompetitionTeamResolver(FantasoccerDbContext database)
{
    public async Task<ResolvedTeam?> ResolveAsync(Guid group, int season, Guid teamId, CancellationToken cancellationToken = default) =>
        (await ResolveManyAsync(group, season, [teamId], cancellationToken)).SingleOrDefault();

    public async Task<ResolvedTeam[]> ResolveManyAsync(Guid group, int season, IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken = default)
    {
        if (teamIds.Count == 0) return [];
        var ids = teamIds.Distinct().ToArray();
        var teams = await database.TeamSeasons.AsNoTracking().Where(x => x.GroupId == group && x.SeasonIndex == season && ids.Contains(x.Id)).ToArrayAsync(cancellationToken);
        var origins = await database.TeamDerivations.AsNoTracking().Where(x => x.GroupId == group && x.SeasonIndex == season && ids.Contains(x.TeamId))
            .ToDictionaryAsync(x => x.TeamId, cancellationToken);
        var authoritativeIds = teams.Select(x => origins.TryGetValue(x.Id, out var origin) ? origin.OriginTeamId : x.Id).Distinct().ToArray();
        var authoritative = await database.TeamSeasons.AsNoTracking().Include(x => x.Owners).ThenInclude(x => x.Membership).ThenInclude(x => x.User)
            .Where(x => x.GroupId == group && authoritativeIds.Contains(x.Id)).AsSplitQuery().ToDictionaryAsync(x => x.Id, cancellationToken);
        return teams.Select(team =>
        {
            var source = authoritative[origins.TryGetValue(team.Id, out var origin) ? origin.OriginTeamId : team.Id];
            return new ResolvedTeam(group, season, team.Id, team.BasketId, team.Name, team.LogoBlobName, team.IsArchived, source.Id, source.BasketId, source.LogoBlobName,
                source.Owners.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.UserId).Select(x => new ResolvedOwner(x.UserId, x.Membership.User.Email,
                    x.Membership.User.OwnerReference, x.Membership.User.DisplayName, x.IsPrimary,
                    x.Membership.Roles != GroupRole.None && x.Membership.User.IsEnabled)).ToArray());
        }).ToArray();
    }

    // Admitted teams of a league season (archived teams are kept for history but
    // excluded unless requested).
    public async Task<ResolvedTeam[]> ParticipantsAsync(Guid group, Guid league, int season, bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var ids = await database.LeagueParticipations.AsNoTracking().Where(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == season)
            .Select(x => x.TeamId).ToArrayAsync(cancellationToken);
        var teams = await ResolveManyAsync(group, season, ids, cancellationToken);
        return teams.Where(x => includeArchived || !x.IsArchived).OrderBy(x => x.Name).ToArray();
    }

    public Task<bool> IsDerivedAsync(Guid group, Guid teamId, CancellationToken cancellationToken = default) =>
        database.TeamDerivations.AnyAsync(x => x.GroupId == group && x.TeamId == teamId, cancellationToken);
}

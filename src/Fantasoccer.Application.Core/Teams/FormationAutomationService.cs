using Fantasoccer.Application.Core.Competitions;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Business;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Application.Core.Teams;

public sealed class FormationAutomationService(FantasoccerDbContext database, IOptimalFormationService optimal,
    CompetitionTeamResolver resolver, TimeProvider clock)
{
    public async Task PrepareNextAsync(int year, CancellationToken cancellationToken = default)
    {
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var next = await database.FootballMatches.Where(x => x.SeasonIndex == year && (x.WasPostponed ? x.OriginalKickoffUtc : x.KickoffUtc) != null).GroupBy(x => x.SerieADay)
            .Select(x => new { Day = x.Key, Start = x.Min(game => game.WasPostponed ? game.OriginalKickoffUtc : game.KickoffUtc) }).Where(x => x.Start > clock.GetUtcNow()).OrderBy(x => x.Start).FirstOrDefaultAsync(cancellationToken);
        if (next is null) return;
        // 1) Roster of the day of every authoritative (autonomous) team: shared by
        //    all its competitions, never duplicated per league.
        var teams = await database.TeamSeasons.Include(x => x.Owners).ThenInclude(x => x.Membership).ThenInclude(x => x.User).Where(x => x.SeasonIndex == year && !x.IsArchived &&
                !database.TeamDerivations.Any(d => d.GroupId == x.GroupId && d.TeamId == x.Id)).AsSplitQuery().ToArrayAsync(cancellationToken);
        foreach (var team in teams)
        {
            if (await database.RosterSnapshots.AnyAsync(x => x.GroupId == team.GroupId && x.SeasonIndex == year && x.TeamId == team.Id && x.Kind == RosterKind.Matchday && x.Day == next.Day, cancellationToken)) continue;
            var source = await database.RosterSnapshots.Include(x => x.Players).ThenInclude(x => x.Player).Include(x => x.Players).ThenInclude(x => x.Club)
                .Include(x => x.AdditionalOwners).ThenInclude(x => x.User).Where(x => x.GroupId == team.GroupId && x.TeamId == team.Id && x.SeasonIndex == year && x.Kind == RosterKind.Season).AsSplitQuery().SingleOrDefaultAsync(cancellationToken);
            if (source is null || team.Owners.SingleOrDefault(x => x.IsPrimary) is not { } primary) continue;
            var snapshot = TeamRosterApplicationService.Copy(source, RosterKind.Matchday, next.Day, null);
            var previous = await database.RosterSnapshots.AsNoTracking().Where(x => x.GroupId == team.GroupId && x.TeamId == team.Id && x.SeasonIndex == year && x.Kind == RosterKind.Matchday && x.Day < next.Day)
                .OrderByDescending(x => x.Day).AsSplitQuery().FirstOrDefaultAsync(cancellationToken);
            var previousPositions = previous?.Players.Where(x => x.Status == 0).ToDictionary(x => x.PlayerId, x => x.Position);
            foreach (var player in snapshot.Players.Where(x => x.Status == 0))
                if (previousPositions?.TryGetValue(player.PlayerId, out var position) == true) player.Position = position;
            snapshot.OwnerUserId = primary.UserId; snapshot.Name = team.Name;
            snapshot.AdditionalOwners = team.Owners.Where(x => !x.IsPrimary).Select(x => new RosterOwner { UserId = x.UserId, User = x.Membership.User }).ToArray();
            // Never reset an existing day's live counter/history. This branch
            // creates only a missing future day from server-owned roster entries.
            snapshot.FormationChanges = 0; snapshot.FormationHistory.Clear(); snapshot.LastUpdateUtc = clock.GetUtcNow();
            database.RosterSnapshots.Add(snapshot);
        }
        await database.SaveChangesAsync(cancellationToken);
        // 2) One lineup per competition with a match that day. Each continues its
        //    own previous lineup; the optional AIgoritmo uses that league's rules.
        var games = await database.FantasyMatches.AsNoTracking().Where(x => x.SeasonIndex == year && x.Matchday.SerieADay == next.Day &&
                database.LeagueSeasons.Any(s => s.GroupId == x.GroupId && s.LeagueId == x.LeagueId && s.SeasonIndex == year && !s.IsArchived && !s.League.IsArchived))
            .Select(x => new { x.GroupId, x.LeagueId, x.HomeTeamId, x.AwayTeamId }).ToArrayAsync(cancellationToken);
        var entries = games.SelectMany(x => new[] { (x.GroupId, x.LeagueId, Team: x.HomeTeamId), (x.GroupId, x.LeagueId, Team: x.AwayTeamId) })
            .Where(x => x.Team is not null).Select(x => (x.GroupId, x.LeagueId, Team: x.Team!.Value)).Distinct().ToArray();
        foreach (var entry in entries)
        {
            if (await database.LeagueLineups.AnyAsync(x => x.GroupId == entry.GroupId && x.LeagueId == entry.LeagueId && x.SeasonIndex == year && x.TeamId == entry.Team && x.Day == next.Day, cancellationToken)) continue;
            var team = await resolver.ResolveAsync(entry.GroupId, year, entry.Team, cancellationToken);
            if (team is null || team.IsArchived) continue;
            var daily = await database.RosterSnapshots.Include(x => x.Players).ThenInclude(x => x.Player).Include(x => x.Players).ThenInclude(x => x.Club).AsNoTracking()
                .Where(x => x.GroupId == entry.GroupId && x.SeasonIndex == year && x.TeamId == team.AuthoritativeTeamId && x.Kind == RosterKind.Matchday && x.Day == next.Day && x.CompetitionId == Guid.Empty)
                .AsSplitQuery().SingleOrDefaultAsync(cancellationToken);
            if (daily is null) continue;
            var previous = await database.LeagueLineups.AsNoTracking().Where(x => x.GroupId == entry.GroupId && x.LeagueId == entry.LeagueId && x.SeasonIndex == year && x.TeamId == entry.Team && x.Day < next.Day)
                .OrderByDescending(x => x.Day).FirstOrDefaultAsync(cancellationToken);
            var positions = LeagueLineupApplicationService.Overlay(daily.Players.Where(x => x.Status == 0), previous)
                .ToDictionary(x => x.PlayerId, x => x.Position, StringComparer.Ordinal);
            var preferences = team.Primary is { } owner ? await database.AccountPreferences.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == owner.UserId, cancellationToken) : null;
            if (preferences?.AutoOptimalFormation == true)
            {
                var rules = await database.LeagueRules.AsNoTracking().SingleAsync(x => x.GroupId == entry.GroupId && x.LeagueId == entry.LeagueId && x.SeasonIndex == year, cancellationToken);
                var league = await database.LeagueSeasons.AsNoTracking().Include(x => x.League).SingleAsync(x => x.GroupId == entry.GroupId && x.LeagueId == entry.LeagueId && x.SeasonIndex == year, cancellationToken);
                var domain = new Team { Name = team.Name, Owner = team.OwnerReference, AdditionalOwners = [],
                    Players = daily.Players.Select(player => { var domainPlayer = CompetitionMapping.Domain(player); if (player.Status == 0) domainPlayer.Position = (FantaSoccerRole)positions[player.PlayerId]; return domainPlayer; }).ToList() };
                var proposal = await optimal.CalculateAsync(domain, year, next.Day, GameApplicationService.Type(league), LeagueRulesMapping.ToDomain(rules), cancellationToken);
                var proposed = proposal.Players.ToDictionary(x => x.PlayerKey, x => (int)x.Position);
                foreach (var player in daily.Players.Where(x => x.Status == 0))
                    if (proposed.TryGetValue(KeyUtils.GetPlayerKey(player.Player.Name), out var position)) positions[player.PlayerId] = position;
            }
            database.LeagueLineups.Add(new() { GroupId = entry.GroupId, LeagueId = entry.LeagueId, SeasonIndex = year, TeamId = entry.Team, Day = next.Day,
                LastUpdateUtc = clock.GetUtcNow(), Players = positions.Select(x => new LeagueLineupPlayer { PlayerId = x.Key, Position = x.Value }).ToList() });
        }
        await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
    }
}

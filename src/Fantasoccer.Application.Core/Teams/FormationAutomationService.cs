using Fantasoccer.Application.Core.Competitions;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Business;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Application.Core.Teams;

public sealed class FormationAutomationService(FantasoccerDbContext database, TeamRosterApplicationService rosters, IOptimalFormationService optimal, TimeProvider clock)
{
    public async Task PrepareNextAsync(int year, CancellationToken cancellationToken = default)
    {
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var next = await database.FootballMatches.Where(x => x.SeasonIndex == year && !x.Delayed && x.KickoffUtc != null).GroupBy(x => x.SerieADay)
            .Select(x => new { Day = x.Key, Start = x.Min(game => game.KickoffUtc) }).Where(x => x.Start > clock.GetUtcNow()).OrderBy(x => x.Start).FirstOrDefaultAsync(cancellationToken);
        if (next is null) return;
        var teams = await database.TeamSeasons.Include(x => x.Owners).ThenInclude(x => x.Membership).ThenInclude(x => x.User).Where(x => x.SeasonIndex == year && !x.IsArchived)
            .AsSplitQuery().ToArrayAsync(cancellationToken);
        foreach (var team in teams)
        {
            if (await database.RosterSnapshots.AnyAsync(x => x.GroupId == team.GroupId && x.SeasonIndex == year && x.TeamId == team.Id && x.Kind == RosterKind.Matchday && x.Day == next.Day, cancellationToken)) continue;
            var source = await database.RosterSnapshots.Include(x => x.Players).ThenInclude(x => x.Player).Include(x => x.Players).ThenInclude(x => x.Club)
                .Include(x => x.AdditionalOwners).ThenInclude(x => x.User).Where(x => x.GroupId == team.GroupId && x.TeamId == team.Id && x.SeasonIndex == year && x.Kind == RosterKind.Season).AsSplitQuery().SingleOrDefaultAsync(cancellationToken);
            if (source is null) continue;
            var snapshot = TeamRosterApplicationService.Copy(source, RosterKind.Matchday, next.Day, null);
            var previous = await database.RosterSnapshots.AsNoTracking().Where(x => x.GroupId == team.GroupId && x.TeamId == team.Id && x.SeasonIndex == year && x.Kind == RosterKind.Matchday && x.Day < next.Day)
                .OrderByDescending(x => x.Day).AsSplitQuery().FirstOrDefaultAsync(cancellationToken);
            var previousPositions = previous?.Players.Where(x => x.Status == 0).ToDictionary(x => x.PlayerId, x => x.Position);
            foreach (var player in snapshot.Players.Where(x => x.Status == 0))
                if (previousPositions?.TryGetValue(player.PlayerId, out var position) == true) player.Position = position;
            snapshot.OwnerUserId = team.Owners.Single(x => x.IsPrimary).UserId; snapshot.Name = team.Name;
            snapshot.AdditionalOwners = team.Owners.Where(x => !x.IsPrimary).Select(x => new RosterOwner { UserId = x.UserId, User = x.Membership.User }).ToArray();
            var preferences = await database.AccountPreferences.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == snapshot.OwnerUserId, cancellationToken);
            if (preferences?.AutoOptimalFormation == true)
            {
                var rules = await rosters.RulesAsync(team, year, null, false, cancellationToken);
                var league = await database.LeagueSeasons.Include(x => x.League).SingleAsync(x => x.GroupId == rules.GroupId && x.LeagueId == rules.LeagueId && x.SeasonIndex == year, cancellationToken);
                var type = (LeagueType)(league.CompetitionType != 0 ? league.CompetitionType : league.League.CompetitionType != 0 ? league.League.CompetitionType : 1);
                var domain = new Team { Name = team.Name, Owner = team.Owners.Single(x => x.IsPrimary).Membership.User.OwnerReference, AdditionalOwners = [], Players = snapshot.Players.Select(CompetitionMapping.Domain).ToList() };
                var proposal = await optimal.CalculateAsync(domain, year, next.Day, type, LeagueRulesMapping.ToDomain(rules), cancellationToken);
                var positions = proposal.Players.ToDictionary(x => x.PlayerKey, x => x.Position);
                foreach (var player in snapshot.Players.Where(x => x.Status == 0))
                    if (positions.TryGetValue(KeyUtils.GetPlayerKey(player.Player.Name), out var position)) player.Position = (int)position;
            }
            // Never reset an existing day's live counter/history. This branch
            // creates only a missing future day from server-owned roster entries.
            snapshot.FormationChanges = 0; snapshot.FormationHistory.Clear(); snapshot.LastUpdateUtc = clock.GetUtcNow();
            database.RosterSnapshots.Add(snapshot);
        }
        await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
    }
}

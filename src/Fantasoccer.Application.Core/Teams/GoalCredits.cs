using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Application.Core.Teams;

// Credits earned with goals scored and conceded. They exist only where the league has them on: by default the main league
// (with the default values), never the others. A team that plays several leagues with credits on collects from each of them.
public static class GoalCredits
{
    public static bool Enabled(LeagueRules rules, bool isMain, LeagueMode mode) => mode != LeagueMode.Derived && (rules.GoalCreditsEnabled ?? isMain);

    public static long Entitlement(LeagueRules rules, int goalsFor, int goalsAgainst) => (long)goalsFor * rules.MoneyForGoal + (long)goalsAgainst * rules.MoneyForSufferedGoal;

    // Active: at least one league with credits on has already a standings snapshot. PerLeague: what each team earned in each of them.
    public sealed record Summary(bool Active, Dictionary<(Guid TeamId, Guid LeagueId), long> PerLeague)
    {
        public long Total(Guid teamId) => PerLeague.Where(x => x.Key.TeamId == teamId).Sum(x => x.Value);
        public long Outside(Guid teamId, Guid leagueId) => PerLeague.Where(x => x.Key.TeamId == teamId && x.Key.LeagueId != leagueId).Sum(x => x.Value);
    }

    public static async Task<Summary> ForTeamsAsync(FantasoccerDbContext database, Guid groupId, int year, Guid[] teamIds, CancellationToken cancellationToken)
    {
        var leagues = await (from participation in database.LeagueParticipations.AsNoTracking()
                             join league in database.Leagues.AsNoTracking() on new { participation.GroupId, participation.LeagueId } equals new { league.GroupId, league.LeagueId }
                             join rules in database.LeagueRules.AsNoTracking() on new { participation.GroupId, participation.LeagueId, participation.SeasonIndex } equals new { rules.GroupId, rules.LeagueId, rules.SeasonIndex }
                             where participation.GroupId == groupId && participation.SeasonIndex == year && teamIds.Contains(participation.TeamId) && !league.IsArchived
                             select new { league.LeagueId, league.IsMain, league.Mode, rules }).Distinct().ToArrayAsync(cancellationToken);
        var perLeague = new Dictionary<(Guid, Guid), long>(); var active = false;
        foreach (var league in leagues.GroupBy(x => x.LeagueId).Select(x => x.First()).Where(x => Enabled(x.rules, x.IsMain, x.Mode) && (x.rules.MoneyForGoal != 0 || x.rules.MoneyForSufferedGoal != 0)))
        {
            var rows = await (from snapshot in database.FantasyStandingSnapshots.AsNoTracking()
                              from stage in snapshot.Stages
                              from team in stage.Teams
                              where snapshot.GroupId == groupId && snapshot.LeagueId == league.LeagueId && snapshot.SeasonIndex == year && snapshot.SnapshotDay == 0 && teamIds.Contains(team.TeamId)
                              select new { team.TeamId, stage.Ordinal, team.GoalsFor, team.GoalsAgainst }).ToArrayAsync(cancellationToken);
            if (!await database.FantasyStandingSnapshots.AnyAsync(x => x.GroupId == groupId && x.LeagueId == league.LeagueId && x.SeasonIndex == year && x.SnapshotDay == 0, cancellationToken)) continue;
            active = true;
            // A team plays one stage row per league: the first one counts, as before.
            foreach (var team in rows.GroupBy(x => x.TeamId).Select(x => x.OrderBy(row => row.Ordinal).First()))
                perLeague[(team.TeamId, league.LeagueId)] = Entitlement(league.rules, team.GoalsFor, team.GoalsAgainst);
        }
        return new(active, perLeague);
    }
}

namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

public static class HallContributionMath
{
    public static HallRankContribution[] FromSnapshot(FantasyStandingSnapshot snapshot, Func<string, Guid> owner) =>
        snapshot.Stages.SelectMany(x => x.Teams).GroupBy(x => owner(x.Owner)).Select(group => new HallRankContribution
        {
            GroupId = snapshot.GroupId, LeagueId = snapshot.LeagueId, SeasonIndex = snapshot.SeasonIndex, OwnerUserId = group.Key,
            Points = group.Sum(x => x.Points), Victories = group.Sum(x => x.Victories), Draws = group.Sum(x => x.Draws), Defeats = group.Sum(x => x.Defeats),
            Goals = group.Sum(x => x.GoalsFor), SufferedGoals = group.Sum(x => x.GoalsAgainst), ValuePoints = group.Sum(x => x.ValuePoints),
            SufferedValuePoints = group.Sum(x => x.SufferedValuePoints), Money = group.Sum(x => x.Money), PlusMoney = group.Sum(x => x.PlusMoney)
        }).ToArray();

    public static bool Matches(HallArchive archive, IEnumerable<HallRankContribution> contributions)
    {
        var rows = contributions.GroupBy(x => x.OwnerUserId).ToDictionary(x => x.Key, x => x.ToArray());
        var ranks = archive.Ranks.GroupBy(x => x.OwnerUserId).ToDictionary(x => x.Key, x => x.ToArray());
        return rows.Keys.Union(ranks.Keys).All(id =>
        {
            var a = rows.GetValueOrDefault(id) ?? []; var b = ranks.GetValueOrDefault(id) ?? [];
            return Math.Abs(a.Sum(x => x.Points) - b.Sum(x => x.Points)) < 0.001f && a.Sum(x => x.Victories) == b.Sum(x => x.Victories) &&
                a.Sum(x => x.Draws) == b.Sum(x => x.Draws) && a.Sum(x => x.Defeats) == b.Sum(x => x.Defeats) && a.Sum(x => x.Goals) == b.Sum(x => x.Goals) &&
                a.Sum(x => x.SufferedGoals) == b.Sum(x => x.SufferedGoals) && Math.Abs(a.Sum(x => x.ValuePoints) - b.Sum(x => x.ValuePoints)) < 0.001f &&
                Math.Abs(a.Sum(x => x.SufferedValuePoints) - b.Sum(x => x.SufferedValuePoints)) < 0.001f && a.Sum(x => x.Money) == b.Sum(x => x.Money) && a.Sum(x => x.PlusMoney) == b.Sum(x => x.PlusMoney);
        });
    }
}

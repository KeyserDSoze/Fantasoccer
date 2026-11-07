namespace Fantasoccer.Application.Core.Football;

public static class LineupKinds
{
    public const string Starter = "starter";
    public const string EstimatedStarter = "estimated-starter";
    public const string Substitute = "substitute";
    public const string Bench = "bench";
    public const string EstimatedBench = "estimated-bench";
    public const string Stand = "stand";
}

public static class LineupSources
{
    public const string Official = "official";
    public const string Live = "live";
    public const string Probable = "probable";
    public const string Unavailable = "unavailable";
}

// One club player as the builder sees it. Role is 0..3 (GK, D, M, F); Appearances counts the earlier matchdays he played or was called up.
public sealed record LineupEntry(string PlayerId, string Name, int Role, bool IsActive, bool IsVisible, int Appearances, VoteDto? Vote, ChanceDto? Chance)
{
    public int ChanceSources => Chance is null ? 0 : (Chance.Fantagazzetta ? 1 : 0) + (Chance.Gazzetta ? 1 : 0) + (Chance.Mediaset ? 1 : 0) + (Chance.Sky ? 1 : 0);
    // Injured or disqualified players are never part of a probable eleven or of an estimated bench.
    public bool Unavailable => Chance is { Status: 1 or 3 };
    public bool Played => Vote is { } vote && (vote.HasVote || vote.IsIn || vote.IsOut);
}

public sealed record LineupPlacement(LineupEntry Entry, int Position, string Kind);
public sealed record SideLineup(string Source, LineupPlacement[] Players);

// Rebuilds the two sides of a real match. Pure: the caller decides who belongs to the club on that date.
public static class SerieALineupBuilder
{
    public const int MinimumProbableFlagged = 8;
    private static readonly int[] BenchTargets = [1, 2, 2, 2];
    private static readonly int[][] BenchSlots = [[4], [5, 6], [7, 8], [9, 10]];
    private const int StandSlot = 11;

    // voted: players listed by the votes feed (official or live) of this club. squad: the other players of the club.
    public static SideLineup Build(string? votesSource, IReadOnlyList<LineupEntry> voted, IReadOnlyList<LineupEntry> squad)
    {
        voted = voted.Where(x => x.Role is >= 0 and <= 3).ToArray();
        squad = squad.Where(x => x.Role is >= 0 and <= 3).ToArray();
        if (votesSource is not null && voted.Count > 0) return FromVotes(votesSource, voted, squad);
        return Probable(voted.Concat(squad).ToArray());
    }

    private static SideLineup FromVotes(string source, IReadOnlyList<LineupEntry> voted, IReadOnlyList<LineupEntry> squad)
    {
        var starters = voted.Where(x => x.Played && x.Vote is not { IsIn: true }).Select(x => (Entry: x, Kind: LineupKinds.Starter)).ToList();
        var substitutes = voted.Where(x => x.Vote is { IsIn: true }).ToList();
        var calledUp = voted.Where(x => !x.Played).ToList();
        var pool = squad.ToList();
        var demoted = new List<LineupEntry>();

        if (starters.Count > 11)
        {
            // Nobody says who started (official votes): the keeper who played most, then the outfield players who played most.
            IOrderedEnumerable<(LineupEntry Entry, string Kind)> Ranked(IEnumerable<(LineupEntry Entry, string Kind)> rows) => rows
                .OrderByDescending(x => x.Entry.Vote is { HasVote: true }).ThenByDescending(x => x.Entry.Appearances).ThenBy(x => x.Entry.Name, StringComparer.Ordinal);
            var keep = Ranked(starters.Where(x => x.Entry.Role == 0)).Take(1).Concat(Ranked(starters.Where(x => x.Entry.Role != 0)))
                .Take(11).ToList();
            demoted.AddRange(starters.Except(keep).Select(x => x.Entry));
            starters = keep;
        }
        else if (starters.Count < 11 && source == LineupSources.Live)
        {
            // Early in a live match the feed can still list everybody without a vote: complete the eleven from the probable lineups.
            var candidates = calledUp.Concat(pool).Where(x => !x.Unavailable).OrderByDescending(Score).ThenBy(x => x.Name, StringComparer.Ordinal).ToList();
            var goalkeepers = starters.Count(x => x.Entry.Role == 0);
            foreach (var candidate in candidates)
            {
                if (starters.Count >= 11) break;
                if (candidate.Role == 0 && goalkeepers >= 1) continue;
                if (candidate.Role == 0) goalkeepers++;
                starters.Add((candidate, LineupKinds.EstimatedStarter));
                calledUp.Remove(candidate);
                pool.Remove(candidate);
            }
        }

        var placements = starters.Select(x => new LineupPlacement(x.Entry, x.Entry.Role, x.Kind)).ToList();
        var sure = substitutes.Select(x => (Entry: x, Kind: LineupKinds.Substitute))
            .Concat(calledUp.Concat(demoted).Select(x => (Entry: x, Kind: LineupKinds.Bench))).ToList();
        AddBench(placements, sure, pool);
        return new(source, Order(placements));
    }

    private static SideLineup Probable(IReadOnlyList<LineupEntry> all)
    {
        if (all.Count(x => x.ChanceSources > 0) < MinimumProbableFlagged) return new(LineupSources.Unavailable, []);
        var flagged = all.Where(x => x.ChanceSources > 0 && !x.Unavailable).OrderByDescending(Score).ThenBy(x => x.Name, StringComparer.Ordinal).ToList();
        var chosen = new List<LineupEntry>();
        var goalkeeper = flagged.FirstOrDefault(x => x.Role == 0);
        if (goalkeeper is not null) chosen.Add(goalkeeper);
        chosen.AddRange(flagged.Where(x => x.Role != 0).Take(goalkeeper is null ? 11 : 10));
        var placements = chosen.Select(x => new LineupPlacement(x, x.Role, LineupKinds.EstimatedStarter)).ToList();
        AddBench(placements, [], all.Except(chosen).ToList());
        return new(LineupSources.Probable, Order(placements));
    }

    private static void AddBench(List<LineupPlacement> placements, IReadOnlyList<(LineupEntry Entry, string Kind)> sure, List<LineupEntry> rest)
    {
        var perRole = new int[4];
        foreach (var item in sure.OrderBy(x => x.Kind == LineupKinds.Substitute ? 0 : 1).ThenByDescending(x => x.Entry.Appearances)
                     .ThenBy(x => x.Entry.Name, StringComparer.Ordinal))
        {
            var role = item.Entry.Role;
            var slots = BenchSlots[role];
            placements.Add(new(item.Entry, slots[Math.Min(perRole[role]++, slots.Length - 1)], item.Kind));
        }
        var listed = sure.Select(x => x.Entry.PlayerId).ToHashSet(StringComparer.Ordinal);
        var remaining = rest.Where(x => !listed.Contains(x.PlayerId)).OrderByDescending(x => x.Appearances).ThenBy(x => x.Name, StringComparer.Ordinal).ToList();
        var stands = new List<LineupEntry>();
        foreach (var candidate in remaining)
        {
            var role = candidate.Role;
            if (!candidate.Unavailable && perRole[role] < BenchTargets[role])
            {
                var slots = BenchSlots[role];
                placements.Add(new(candidate, slots[Math.Min(perRole[role]++, slots.Length - 1)], LineupKinds.EstimatedBench));
            }
            else stands.Add(candidate);
        }
        placements.AddRange(stands.Select(x => new LineupPlacement(x, StandSlot, LineupKinds.Stand)));
    }

    private static int Score(LineupEntry entry) => entry.ChanceSources * 1000 + entry.Appearances;

    private static LineupPlacement[] Order(IEnumerable<LineupPlacement> placements) =>
        placements.OrderBy(x => x.Position).ThenBy(x => x.Kind == LineupKinds.Substitute ? 0 : 1).ThenByDescending(x => x.Entry.Appearances)
            .ThenBy(x => x.Entry.Name, StringComparer.Ordinal).ToArray();
}

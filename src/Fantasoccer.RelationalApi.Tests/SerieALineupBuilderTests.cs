using Fantasoccer.Application.Core.Football;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed class SerieALineupBuilderTests
{
    private static VoteDto Vote(bool hasVote, bool isIn = false, bool isOut = false) =>
        new(0, hasVote ? 6f : 0f, false, 0, 0, 0, 0, 0, 0, 0, 0, false, hasVote, isOut, isIn, false);
    private static ChanceDto Chance(int sources, int status = 0) => new(sources > 0, sources > 1, sources > 2, sources > 3, status, null);
    private static LineupEntry Entry(string id, int role, VoteDto? vote = null, ChanceDto? chance = null, int appearances = 0) =>
        new(id, id, role, true, true, appearances, vote, chance);

    private static LineupEntry[] Eleven(string prefix, Func<int, VoteDto?> vote) =>
        Enumerable.Range(0, 11).Select(i => Entry($"{prefix}{i}", i == 0 ? 0 : 1 + i % 3, vote(i))).ToArray();

    [Fact]
    public void Votes_give_starters_substitutes_and_called_up_players_and_the_estimated_bench_follows_the_fantacalcio_slots()
    {
        var voted = Eleven("s", _ => Vote(true)).Concat([Entry("in", 3, Vote(true, isIn: true)), Entry("called", 0, Vote(false))]).ToArray();
        var squad = new[]
        {
            Entry("d-many", 1, appearances: 9), Entry("d-some", 1, appearances: 5), Entry("d-few", 1, appearances: 1),
            Entry("m1", 2, appearances: 7), Entry("m2", 2, appearances: 6), Entry("m3", 2, appearances: 2),
            Entry("f1", 3, appearances: 8), Entry("f2", 3, appearances: 3),
        };
        var side = SerieALineupBuilder.Build(LineupSources.Official, voted, squad);
        Assert.Equal(LineupSources.Official, side.Source);
        Assert.Equal(11, side.Players.Count(x => x.Kind == LineupKinds.Starter));
        Assert.All(side.Players.Where(x => x.Kind == LineupKinds.Starter), x => Assert.Equal(x.Entry.Role, x.Position));
        Assert.Equal(LineupKinds.Substitute, side.Players.Single(x => x.Entry.PlayerId == "in").Kind);
        Assert.Equal(LineupKinds.Bench, side.Players.Single(x => x.Entry.PlayerId == "called").Kind);
        var estimated = side.Players.Where(x => x.Kind == LineupKinds.EstimatedBench).Select(x => x.Entry.PlayerId).Order().ToArray();
        // Goalkeeper slot taken by the called-up keeper; two defenders, two midfielders and one forward (the substitute counts) are added.
        Assert.Equal(new[] { "d-many", "d-some", "f1", "m1", "m2" }.Order(), estimated);
        Assert.Equal(new[] { "d-few", "f2", "m3" }.Order(), side.Players.Where(x => x.Kind == LineupKinds.Stand).Select(x => x.Entry.PlayerId).Order());
        Assert.All(side.Players.Where(x => x.Kind == LineupKinds.Stand), x => Assert.Equal(11, x.Position));
    }

    [Fact]
    public void Official_votes_that_say_nothing_about_who_started_still_give_eleven_on_the_pitch_with_one_keeper()
    {
        // Fifteen voted players, no in/out information: the keeper who played most, then the outfield players who played most.
        var voted = new[] { Entry("gk-main", 0, Vote(true), appearances: 9), Entry("gk-second", 0, Vote(true), appearances: 1) }
            .Concat(Enumerable.Range(0, 13).Select(i => Entry($"o{i:00}", 1 + i % 3, Vote(true), appearances: 13 - i))).ToArray();
        var side = SerieALineupBuilder.Build(LineupSources.Official, voted, []);
        var starters = side.Players.Where(x => x.Kind == LineupKinds.Starter).ToArray();
        Assert.Equal(11, starters.Length);
        Assert.Equal("gk-main", Assert.Single(starters, x => x.Position == 0).Entry.PlayerId);
        Assert.Equal(Enumerable.Range(0, 10).Select(i => $"o{i:00}"), starters.Where(x => x.Position != 0).Select(x => x.Entry.PlayerId).Order());
        // The others are called up: on the bench, not lost.
        Assert.Equal(4, side.Players.Count(x => x.Kind == LineupKinds.Bench));
        Assert.Equal(LineupKinds.Bench, side.Players.Single(x => x.Entry.PlayerId == "gk-second").Kind);
    }

    [Fact]
    public void An_early_live_feed_without_votes_completes_the_eleven_from_the_probable_lineups()
    {
        var voted = Eleven("v", _ => Vote(false));
        var squad = Enumerable.Range(0, 5).Select(i => Entry($"x{i}", 2, chance: Chance(3), appearances: i)).ToArray();
        var side = SerieALineupBuilder.Build(LineupSources.Live, voted, squad);
        var starters = side.Players.Where(x => x.Position <= 3).ToArray();
        Assert.Equal(11, starters.Length);
        Assert.Equal(1, starters.Count(x => x.Position == 0));
        Assert.All(starters, x => Assert.Equal(LineupKinds.EstimatedStarter, x.Kind));
        Assert.Contains(starters, x => x.Entry.PlayerId == "x4");
    }

    [Fact]
    public void Probable_lineup_needs_enough_flagged_players_skips_unavailable_ones_and_takes_a_single_goalkeeper()
    {
        var few = Enumerable.Range(0, 7).Select(i => Entry($"p{i}", 1, chance: Chance(1))).ToArray();
        Assert.Equal(LineupSources.Unavailable, SerieALineupBuilder.Build(null, [], few).Source);

        var squad = new List<LineupEntry> { Entry("gk1", 0, chance: Chance(4)), Entry("gk2", 0, chance: Chance(1)), Entry("hurt", 3, chance: Chance(4, status: 1)) };
        squad.AddRange(Enumerable.Range(0, 11).Select(i => Entry($"o{i}", 1 + i % 3, chance: Chance(2), appearances: i)));
        var side = SerieALineupBuilder.Build(null, [], squad);
        Assert.Equal(LineupSources.Probable, side.Source);
        var starters = side.Players.Where(x => x.Position <= 3).ToArray();
        Assert.Equal(11, starters.Length);
        Assert.Equal("gk1", starters.Single(x => x.Position == 0).Entry.PlayerId);
        Assert.DoesNotContain(starters, x => x.Entry.PlayerId == "hurt");
        Assert.Equal(LineupKinds.Stand, side.Players.Single(x => x.Entry.PlayerId == "hurt").Kind);
    }

    [Fact]
    public void More_than_eleven_starters_demote_the_extras_to_the_bench()
    {
        var voted = Eleven("s", _ => Vote(true)).Concat([Entry("extra", 2, Vote(true))]).ToArray();
        var side = SerieALineupBuilder.Build(LineupSources.Official, voted, []);
        Assert.Equal(11, side.Players.Count(x => x.Kind == LineupKinds.Starter));
        Assert.Equal(12, side.Players.Length);
    }
}

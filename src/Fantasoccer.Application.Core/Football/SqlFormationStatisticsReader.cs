using Fantasoccer.Business;
using Fantasoccer.Domain;
using Fantasoccer.Infrastructure.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Application.Core.Football;

public sealed class SqlFormationStatisticsReader(FantasoccerDbContext database) : IFormationStatisticsReader
{
    public async Task<FormationStatistics> ReadAsync(int year, CancellationToken cancellationToken = default)
    {
        var statistics = await database.FootballPlayerStatistics.AsNoTracking().Include(x => x.Player).Include(x => x.Club)
            .Where(x => x.SeasonIndex == year).AsSplitQuery().ToArrayAsync(cancellationToken);
        var rank = await database.FootballClubRanks.AsNoTracking().Include(x => x.Club).Where(x => x.SeasonIndex == year).OrderBy(x => x.Ordinal).ToArrayAsync(cancellationToken);
        return new(statistics.Select(x => new StatPlayer { Name = x.Player.Name, Role = (Role)x.PlayerRole, RealTeam = FootballMapping.DomainClub(x.Club),
            IsActive = x.IsActive, Visible = x.IsVisible, Summatory = x.Summatory, FantaSummatory = x.FantaSummatory, WithVote = x.WithVote,
            WithoutVote = x.WithoutVote, NoPlayed = x.NoPlayed, WithSpecial = x.WithSpecial, Goal = x.Goals, Penalty = x.Penalties, Assist = x.Assists,
            StoppedPenalty = x.StoppedPenalties, SufferedGoal = x.SufferedGoals, WrongedPenalty = x.WrongedPenalties, OwnGoal = x.OwnGoals,
            YellowCard = x.YellowCards, RedCard = x.RedCards, IsEnough = x.EnoughVotes, ManOfTheMatch = x.ManOfTheMatch, Injured = x.Injured,
            AllGames = x.HasGameHistory ? x.Games.OrderBy(game => game.Ordinal).Select(game => new StatPlayerGame { SerieADay = game.Day, Vote = game.Vote, Positiveness = game.Positiveness }).ToList() : null
        }).ToArray(), rank.Select(x => new RealRankedTeam { Name = x.Club.Name, Point = x.Points, Victories = x.Victories, Draws = x.Draws,
            Defeats = x.Defeats, Goal = x.GoalsFor, SufferedGoal = x.GoalsAgainst, Played = x.Played }).ToArray());
    }
}

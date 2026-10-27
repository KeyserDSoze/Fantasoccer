using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Application.Core.Competitions;

public sealed partial class CompetitionApplicationService
{
    private async Task<FantasyRankDto?> HistoricalRankAsync(LeagueSeason season, Guid group, Guid league, int year, int until,
        CancellationToken cancellationToken)
    {
        var calendar = await database.FantasyCalendars.AsNoTracking().Include(x => x.Stages).ThenInclude(x => x.Days).ThenInclude(x => x.Games)
            .Where(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year).AsSplitQuery().SingleOrDefaultAsync(cancellationToken);
        if (calendar is null) return null;
        var rules = await database.LeagueRules.AsNoTracking().SingleAsync(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year, cancellationToken);
        var type = (LeagueType)(season.CompetitionType != 0 ? season.CompetitionType : season.League.CompetitionType != 0 ? season.League.CompetitionType : 1);
        var calculator = calculators.Create(type.ToString()) ?? throw new ApplicationFault(409, "league_calculator_unavailable", "Calcolatore non disponibile.");
        var identities = new Dictionary<(string Stage, string Team), HistoricalRankTeam>();
        var domain = new Calendar { Year = year, Rounds = new() };
        foreach (var stage in calendar.Stages.OrderBy(x => x.Ordinal))
        {
            var days = new List<Day>();
            foreach (var day in stage.Days.Where(x => x.SerieADay <= until).OrderBy(x => x.Ordinal))
            {
                var games = new List<CalendarGame>();
                foreach (var game in day.Games.OrderBy(x => x.Ordinal))
                {
                    if (game.Result is { IsCancelled: false } result && (result.Home is null || result.Away is null))
                        throw new ApplicationFault(409, "historical_rank_result_unavailable", "Un risultato del calendario è incompleto: impossibile ricostruire la classifica.");
                    // Unassigned bracket placeholders cannot identify a team.
                    // A stored played result without those identities is not
                    // reconstructed by guessing owners/names from another year.
                    if (game.HomeTeamId is null || game.AwayTeamId is null)
                    {
                        if (game.Result is { IsCancelled: false }) throw new ApplicationFault(409, "historical_rank_identity_unavailable",
                            "Un risultato del calendario non identifica entrambe le squadre: impossibile ricostruire la classifica.");
                        continue;
                    }
                    var home = game.HomeTeamId.Value.ToString("D"); var away = game.AwayTeamId.Value.ToString("D");
                    identities.TryAdd((stage.Stage, home), new(game.HomeTeamId.Value, game.HomeBasketId!.Value, game.HomeName, game.HomeOwner ?? ""));
                    identities.TryAdd((stage.Stage, away), new(game.AwayTeamId.Value, game.AwayBasketId!.Value, game.AwayName, game.AwayOwner ?? ""));
                    // Calculate with stable annual team IDs, not owner casing or
                    // display names. Source labels are restored only in the DTO.
                    games.Add(new() { Id = game.Id.ToString(), Number = game.Number, Home = game.HomeName, Away = game.AwayName,
                        HomeOwner = home, AwayOwner = away, Result = CompetitionMapping.Domain(game.Result) });
                }
                if (games.Count > 0) days.Add(new() { Number = day.FantasyTurn, SerieADay = day.SerieADay, Games = games });
            }
            if (days.Count > 0) domain.Rounds.Add(stage.Stage, days);
        }
        var rank = calculator.GetRank(LeagueRulesMapping.ToDomain(rules), domain);
        return new(rank.SerieADay, rank.RankedTeams.ToDictionary(x => x.Key, x => x.Value.Select(team =>
        {
            var identity = identities[(x.Key, team.Owner)];
            // PlusMoney is a theoretical entitlement from saved results/rules,
            // not an actual credit. Historical balances cannot be inferred.
            return new RankedTeamDto(identity.TeamId, identity.BasketId, identity.Name, identity.Owner, team.Point,
                team.Victories, team.Draws, team.Defeats, team.Goal, team.SufferedGoal, team.ValuePoint, team.SufferedValuePoint, team.PlusMoney, null);
        }).ToArray()), until) { IsReconstructed = true };
    }

    private sealed record HistoricalRankTeam(Guid TeamId, Guid BasketId, string Name, string Owner);
}

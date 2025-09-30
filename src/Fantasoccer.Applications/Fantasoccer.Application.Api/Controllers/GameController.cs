using Fantasoccer.Business;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepositoryFramework;

namespace Fantasoccer.Application.Api.Controllers
{
    [Authorize(Policy = "authenticated")]
    [ApiController]
    [Route("[controller]/[action]")]
    public class GameController : ControllerBase
    {
        private readonly IFactory<ILeagueCalculator> _leagueCalculatorFactory;
        private readonly ITeamCalculator _teamCalculator;
        private readonly IRepository<Calendar, LeagueKey> _calendar;
        private readonly IRepository<RealCalendar, int> _realCalendar;
        private readonly IRepository<Group, string> _groups;
        private readonly IRepository<Team, TeamKey> _teamRepository;
        private readonly IRepository<Team, TeamDayKey> _teamDayRepository;

        public GameController(IFactory<ILeagueCalculator> leagueCalculatorFactory,
            ITeamCalculator teamCalculator,
            IRepository<Calendar, LeagueKey> calendar,
            IRepository<RealCalendar, int> realCalendar,
            IRepository<Group, string> groups,
            IRepository<Team, TeamKey> teamRepository,
            IRepository<Team, TeamDayKey> teamDayRepository)
        {
            _leagueCalculatorFactory = leagueCalculatorFactory;
            _teamCalculator = teamCalculator;
            _calendar = calendar;
            _realCalendar = realCalendar;
            _groups = groups;
            _teamRepository = teamRepository;
            _teamDayRepository = teamDayRepository;
        }
        [HttpGet]
        public async Task<GameWrapper?> GetAsync([FromQuery] string groupId, [FromQuery] string leagueId, [FromQuery] int year, [FromQuery] string gameId)
        {
            var group = await _groups.GetAsync(groupId);
            if (group != null)
            {
                var league = group.GetAnnualLeague(leagueId, year);
                if (league != null)
                {
                    var calendar = await _calendar.GetAsync(new LeagueKey { Group = groupId, League = leagueId, Year = year });
                    if (calendar != null)
                    {
                        var game = calendar.GetGameById(gameId);
                        if (game.Value != null)
                        {
                            var leagueSettings = league.Settings;
                            var leagueType = league.Type == LeagueType.Null ? LeagueType.League : league.Type;
                            var leagueCalculator = _leagueCalculatorFactory.Create(leagueType.ToString());
                            var realCalendar = await _realCalendar.GetAsync(year);
                            var nextSerieA = realCalendar?.NextDay?.SerieADay ?? 39;
                            var hasNoResult = game.Value.HasNoResult == true;
                            var canEdit = game.SerieADay >= nextSerieA;
                            await _teamCalculator.BuildRequestAsync(year, game.SerieADay, leagueType, leagueSettings!, true, !canEdit, true);
                            var isLive = game.SerieADay == realCalendar?.LiveSerieADay;
                            var teams = new List<LiveTeam>();
                            foreach (var owner in GetOwners())
                            {
                                var basketId = group.GetBasketId(owner, year)!;
                                var isInHome = game.Value.HomeOwner == owner;
                                var teamDayKey = new TeamDayKey
                                {
                                    Year = year,
                                    Email = owner,
                                    Group = groupId,
                                    BasketId = basketId,
                                    Day = game.SerieADay
                                };
                                var team = await _teamDayRepository.GetAsync(teamDayKey);
                                if (team == null && canEdit)
                                {
                                    team = await _teamRepository.GetAsync(new TeamKey
                                    {
                                        Year = year,
                                        Email = owner,
                                        Group = groupId,
                                        BasketId = basketId
                                    });
                                }
                                var currentPlayers = _teamCalculator.Enrich(team?.ActivePlayers ?? []) ?? [];
                                if (!canEdit)
                                    currentPlayers = leagueCalculator?.GetRightFormation(currentPlayers) ?? [];
                                teams.Add(new LiveTeam
                                {
                                    Name = team?.Name ?? (isInHome ? game.Value.Home : game.Value.Away),
                                    Owner = team?.Owner ?? owner,
                                    Players = [.. currentPlayers.Select(x => new LivePlayer
                                    {
                                        Current = x.Current,
                                        Chance = x.Chance,
                                        CurrentPosition = x.CurrentPosition,
                                        FinalValue = x.FinalValue,
                                        RealGame = x.RealGame,
                                        Vote = x.Vote,
                                    })],
                                    LastUpdate = team?.LastUpdate ?? DateTime.UtcNow,
                                });
                                if (!canEdit && hasNoResult)
                                {
                                    if (game.Value.Result == null)
                                    {
                                        game.Value.Result = new();
                                    }
                                    if (team?.ActivePlayers != null)
                                    {
                                        if (isInHome)
                                        {
                                            game.Value.Result.Home = _teamCalculator.CalculatePoint(team.ActivePlayers);
                                        }
                                        else
                                        {
                                            game.Value.Result.Away = _teamCalculator.CalculatePoint(team.ActivePlayers);
                                        }
                                    }
                                }
                            }
                            IEnumerable<string> GetOwners()
                            {
                                yield return game.Value.HomeOwner;
                                yield return game.Value.AwayOwner;
                            }
                            if (!canEdit && game.Value.Result != null)
                            {
                                if (_teamCalculator.LeagueSetting != null)
                                    game.Value.Result.CalculateGoals(_teamCalculator.LeagueSetting);
                            }
                            return new GameWrapper
                            {
                                Game = game.Value,
                                SerieADay = game.SerieADay,
                                Teams = teams
                            };
                        }
                    }
                }
            }
            return default;
        }
    }
}

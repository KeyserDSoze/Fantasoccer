using System.Security.Cryptography;
using Fantasoccer.Business;
using Fantasoccer.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepositoryFramework;

namespace Fantasoccer.Application.Api.Controllers
{
    [Authorize(Policy = "authenticated")]
    [ApiController]
    [Route("[controller]/[action]")]
    public class LeagueManagerController : ControllerBase
    {
        private readonly IVotePlayerGenerator _votePlayerGenerator;
        private readonly IStatsPlayerGenerator _statPlayerGenerator;
        private readonly IRepository<RealCalendar, int> _realCalendar;
        private readonly IRankCalculator _rankCalculator;
        private readonly ITeamCalculator _teamCalculator;
        private readonly IFactory<ILeagueCalculator> _leagueCalculatorFactory;
        private readonly IRepository<Group, string> _groups;
        private readonly IRepository<Calendar, LeagueKey> _calendar;
        private readonly IRepository<Rank, LeagueKey> _rank;
        private readonly IRepository<RealPlayersWrapper, int> _realPlayers;
        private readonly IRepository<RealPlayersWrapper, LeagueKey> _randomizedRealPlayers;
        private readonly IRepository<Team, TeamKey> _team;
        private readonly IRepository<Team, TeamDayKey> _dailyTeam;

        public LeagueManagerController(
            IVotePlayerGenerator votePlayerGenerator,
            IStatsPlayerGenerator statPlayerGenerator,
            IRepository<RealCalendar, int> realCalendar,
            IRankCalculator rankCalculator,
            ITeamCalculator teamCalculator,
            IFactory<ILeagueCalculator> leagueCalculatorFactory,
            IRepository<Calendar, LeagueKey> calendar,
            IRepository<Group, string> groups,
            IRepository<Rank, LeagueKey> rank,
            IRepository<RealPlayersWrapper, int> realPlayers,
            IRepository<RealPlayersWrapper, LeagueKey> randomizedRealPlayers,
            IRepository<Team, TeamKey> team,
            IRepository<Team, TeamDayKey> dailyTeam
            )
        {
            _votePlayerGenerator = votePlayerGenerator;
            _statPlayerGenerator = statPlayerGenerator;
            _realCalendar = realCalendar;
            _rankCalculator = rankCalculator;
            _teamCalculator = teamCalculator;
            _leagueCalculatorFactory = leagueCalculatorFactory;
            _calendar = calendar;
            _groups = groups;
            _rank = rank;
            _realPlayers = realPlayers;
            _randomizedRealPlayers = randomizedRealPlayers;
            _team = team;
            _dailyTeam = dailyTeam;
        }
        [HttpGet]
        public async ValueTask<bool> CreateCalendarAndRankAsync([FromQuery] string groupId, [FromQuery] string leagueId, [FromQuery] int year)
        {
            var group = await _groups.GetAsync(groupId);
            if (group != null)
            {
                var league = group.Leagues.First(x => x.Id == leagueId);
                var actualLeague = group.GetAnnualLeague(leagueId, year);
                if (actualLeague != null)
                {
                    var leagueCalculator = _leagueCalculatorFactory.Create(actualLeague.Type.ToString())!;
                    var leagueKey = new LeagueKey()
                    {
                        Group = groupId,
                        League = leagueId,
                        Year = year
                    };
                    var calendar = await _calendar.GetAsync(leagueKey);
                    if (calendar == null || calendar != null)
                    {
                        var teams = new List<AnnualTeam>();
                        foreach (var basket in group.Baskets.Where(x => league.BasketsId.Contains(x.Id)))
                        {
                            foreach (var team in basket.Years.First(x => x.Year == year).Teams)
                                teams.Add(team);
                        }
                        if (actualLeague.Type == LeagueType.Cup)
                        {
                            var worthTeams = new List<AnnualTeam>();
                            foreach (var mainLeague in group.Leagues.Where(x => x.IsMain))
                            {
                                var lastYear = mainLeague.Years.OrderByDescending(x => x.Year).Skip(1).FirstOrDefault();
                                if (lastYear != null)
                                {
                                    var lastRank = await _rank.GetAsync(new LeagueKey()
                                    {
                                        Group = groupId,
                                        League = mainLeague.Id,
                                        Year = lastYear.Year
                                    });
                                    if (lastRank != null)
                                    {
                                        var firstEight = lastRank.RankedTeams.SelectMany(x => x.Value).OrderByDescending(x => x.Point).Take(8).ToList();
                                        foreach (var team in teams)
                                        {
                                            if (firstEight.Any(x => x.Owner == team.Owner))
                                            {
                                                worthTeams.Add(team);
                                            }
                                        }
                                    }
                                }
                            }
                            foreach (var worthedTeam in worthTeams)
                            {
                                teams.Remove(worthedTeam);
                            }
                            if (worthTeams.Count < 16)
                            {
                                worthTeams.AddRange(Randomize(teams).Take(16 - worthTeams.Count));
                            }
                            teams = worthTeams;

                            List<AnnualTeam> Randomize(List<AnnualTeam> teams)
                            {
                                var teamsToAdd = new List<AnnualTeam>();
                                foreach (var team in teams)
                                    teamsToAdd.Add(team);
                                List<AnnualTeam> randomizedTeams = new();
                                while (teamsToAdd.Count > 0)
                                {
                                    var team = teamsToAdd[GetRandom(teamsToAdd.Count)];
                                    randomizedTeams.Add(team);
                                    teamsToAdd.Remove(team);
                                }
                                return randomizedTeams;
                            }
                            int GetRandom(int max)
                            {
                                var value = RandomNumberGenerator.GetInt32(0, max);
                                if (max == value)
                                    return max - 1;
                                return value;
                            }
                        }
                        calendar = leagueCalculator.GetRandomCalendar(year, actualLeague, teams);
                        await _calendar.UpdateAsync(leagueKey, calendar);
                    }
                    if (calendar != null)
                    {
                        var rank = leagueCalculator.GetRank(actualLeague?.Settings ?? LeagueSetting.Default, calendar);
                        if (await _rank.ExistAsync(leagueKey))
                        {
                            await _rank.UpdateAsync(leagueKey, rank);
                        }
                        else
                        {
                            await _rank.InsertAsync(leagueKey, rank);
                        }
                        return true;
                    }
                }
            }
            return false;
        }
        [HttpGet]
        public async ValueTask<bool> RandomizePlayerForAuctionsAsync([FromQuery] string groupId, [FromQuery] string leagueId, [FromQuery] int year)
        {
            var allPlayersWrapper = await _realPlayers.GetAsync(year);
            if (allPlayersWrapper?.Players != null)
            {
                var randomizedPlayers = allPlayersWrapper.Players
                    .OrderBy(x => x.Role)
                    .ThenBy(x => RandomNumberGenerator.GetInt32(0, int.MaxValue))
                    .ToList();
                foreach (var player in randomizedPlayers)
                {
                    player.Visible = false;
                }
                var leagueKey = new LeagueKey()
                {
                    Group = groupId,
                    League = leagueId,
                    Year = year
                };
                if (await _randomizedRealPlayers.ExistAsync(leagueKey))
                    await _randomizedRealPlayers.UpdateAsync(
                        leagueKey,
                        new RealPlayersWrapper
                        {
                            Players = randomizedPlayers
                        });
                else
                    await _randomizedRealPlayers.InsertAsync(
                        leagueKey,
                        new RealPlayersWrapper
                        {
                            Players = randomizedPlayers
                        });
                return true;
            }
            return false;
        }
        [HttpGet]
        public async ValueTask<bool> RecalculateAsync([FromQuery] string groupId, [FromQuery] string leagueId, [FromQuery] int year, [FromQuery] int dayToRecalculate, [FromQuery] bool onlyLastDay = false, [FromQuery] bool preciseDay = false)
        {
            var group = await _groups.GetAsync(groupId);
            if (group != null)
            {
                var leagueKey = new LeagueKey()
                {
                    Group = groupId,
                    League = leagueId,
                    Year = year
                };
                var calendar = await _calendar.GetAsync(leagueKey);
                var realCalendar = await _realCalendar.GetAsync(year);
                var lastDay = realCalendar?.LastDay;
                var yearlyLeague = group.GetAnnualLeague(leagueId, year);
                var leagueType = yearlyLeague!.Type;
                var settings = yearlyLeague?.Settings;
                if (leagueType == LeagueType.Null)
                    leagueType = group.Leagues.First(x => x.Id == leagueId).Type;
                _teamCalculator.SetLeagueSettings(leagueType, settings ?? LeagueSetting.Default);
                if (settings != null)
                {
                    foreach (var round in calendar.Rounds)
                    {
                        foreach (var day in round.Value)
                        {
                            if (day.Number <= settings.CancelledDay)
                            {
                                foreach (var game in day.Games)
                                {
                                    if (game.Result != null)
                                        game.Result.IsCancelled = true;
                                    else
                                        game.Result = new GameResult { IsCancelled = true };
                                }
                            }
                            else if (!onlyLastDay || (!preciseDay && day.SerieADay == lastDay.SerieADay) || (preciseDay && day.SerieADay == dayToRecalculate))
                            {
                                await _teamCalculator.BuildRequestAsync(yearlyLeague.Year, day.SerieADay, leagueType, yearlyLeague.Settings, false, true, false);
                                if (_teamCalculator.Official == null || _teamCalculator.IsLive)
                                    return false;
                                foreach (var game in day.Games)
                                {
                                    var homeTeamKey = new TeamDayKey
                                    {
                                        Day = day.SerieADay,
                                        Email = game.HomeOwner,
                                        Group = groupId,
                                        BasketId = group.GetBasketId(game.HomeOwner, year)!,
                                        Year = year
                                    };
                                    var homeTeam = await GetTeamAsync(game.HomeOwner, await _dailyTeam.GetAsync(homeTeamKey), homeTeamKey);
                                    var awayTeamKey = new TeamDayKey
                                    {
                                        Day = day.SerieADay,
                                        Email = game.AwayOwner,
                                        Group = groupId,
                                        BasketId = group.GetBasketId(game.AwayOwner, year)!,
                                        Year = year
                                    };
                                    var awayTeam = await GetTeamAsync(game.AwayOwner, await _dailyTeam.GetAsync(awayTeamKey), awayTeamKey);
                                    var homePoint = _teamCalculator.CalculatePoint(homeTeam.ActivePlayers);
                                    var awayPoint = _teamCalculator.CalculatePoint(awayTeam.ActivePlayers);
                                    var result = new GameResult
                                    {
                                        Home = homePoint,
                                        Away = awayPoint,
                                    };
                                    result.CalculateGoals(yearlyLeague.Settings);
                                    game.Result = result;

                                    async Task<Team?> GetTeamAsync(string owner, Team? team, TeamDayKey teamDayKey)
                                    {
                                        if (team != null)
                                            return team;
                                        team = await _team.GetAsync(new TeamKey
                                        {
                                            Group = teamDayKey.Group,
                                            Year = teamDayKey.Year,
                                            BasketId = teamDayKey.BasketId,
                                            Email = owner
                                        });
                                        foreach (var player in team.ActivePlayers)
                                        {
                                            player.Position = FantaSoccerRole.Tribune;
                                        }
                                        await _dailyTeam.UpdateAsync(teamDayKey, team);
                                        return team;
                                    }
                                }
                            }
                        }
                    }
                    var leagueCalculator = _leagueCalculatorFactory.Create(leagueType.ToString())!;
                    await _calendar.UpdateAsync(leagueKey, calendar);
                    var rank = leagueCalculator.GetRank(yearlyLeague.Settings, calendar);
                    await _rank.UpdateAsync(leagueKey, rank);
                    return true;
                }
            }
            return false;
        }
    }
}

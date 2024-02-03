using System.Timers;
using Fantasoccer.Business;
using Fantasoccer.Business.IngestionData;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;

namespace Fantasoccer.Engine.IngestionData
{
    public class LiveJob : IBackgroundJob
    {
        private readonly IFantasoccerLogger _logger;
        private readonly ITeamCalculator _teamCalculator;
        private readonly IRankCalculator _rankCalculator;
        private readonly IRepository<Group, string> _groupRepository;
        private readonly IRepository<LiveGroup, string> _liveGroupRepository;
        private readonly IRepository<Calendar, LeagueKey> _calendarRepository;
        private readonly IRepository<Rank, LeagueKey> _rankRepository;
        private readonly IRepository<Team, TeamDayKey> _teamDayRepository;
        private readonly IRepository<RealCalendar, int> _serieACalendar;
        public LiveJob(IFantasoccerLogger logger,
            ITeamCalculator teamCalculator,
            IRankCalculator rankCalculator,
            IRepository<Group, string> groupRepository,
            IRepository<LiveGroup, string> liveGroupRepository,
            IRepository<Calendar, LeagueKey> calendarRepository,
            IRepository<Rank, LeagueKey> rankRepository,
            IRepository<Team, TeamDayKey> teamDayRepository,
            IRepository<RealCalendar, int> serieACalendar)
        {
            _logger = logger;
            _teamCalculator = teamCalculator;
            _rankCalculator = rankCalculator;
            _groupRepository = groupRepository;
            _liveGroupRepository = liveGroupRepository;
            _calendarRepository = calendarRepository;
            _rankRepository = rankRepository;
            _teamDayRepository = teamDayRepository;
            _serieACalendar = serieACalendar;
        }
        public async Task ActionToDoAsync()
        {
            var actualYear = FantacalcioTime.ActualYear;
            var realCalendar = await _serieACalendar.GetAsync(actualYear).NoContext();
            if (realCalendar != null)
            {
                var liveDay = realCalendar.LiveDay ?? realCalendar.NextDay;
                if (liveDay != default)
                {
                    await _teamCalculator.BuildRequestAsync(FantacalcioTime.ActualYear, liveDay.SerieADay, LeagueSetting.Default, false, true, false);
                    foreach (var group in await _groupRepository.ToListAsEntityAsync())
                    {
                        var liveGroup = new LiveGroup
                        {
                            Name = group.Name,
                            Leagues = new()
                        };
                        foreach (var league in group.Leagues.Where(x => x.Years.Exists(t => t.Year == actualYear)))
                        {
                            var leagueSettings = group.GetAnnualLeague(league.Id, actualYear)?.Settings ?? LeagueSetting.Default;
                            _teamCalculator.SetLeagueSettings(leagueSettings);
                            var leagueKey = new LeagueKey { Group = group.Id, League = league.Id, Year = actualYear };
                            var calendar = await _calendarRepository.GetAsync(leagueKey).NoContext();
                            if (calendar != null)
                            {
                                var liveLeague = new LiveLeague
                                {
                                    Id = league.Id,
                                    Name = league.Name,
                                    Rounds = new()
                                };
                                foreach (var round in calendar.Rounds)
                                {
                                    var day = round.Value.FirstOrDefault(x => x.SerieADay == liveDay.SerieADay);
                                    if (day != null)
                                    {
                                        liveLeague.Rounds.Add(round.Key, day);
                                        foreach (var game in day.Games)
                                        {
                                            if (game.Result?.IsCancelled == true)
                                                continue;
                                            var homeTeamKey = new TeamDayKey
                                            {
                                                Year = actualYear,
                                                Group = group.Id,
                                                Day = day.SerieADay,
                                                BasketId = group.GetBasketId(game.HomeOwner)!,
                                                Email = game.HomeOwner
                                            };
                                            var homeTeam = await _teamDayRepository.GetAsync(homeTeamKey);
                                            var awayTeamKey = new TeamDayKey
                                            {
                                                Year = actualYear,
                                                Group = group.Id,
                                                Day = day.SerieADay,
                                                BasketId = group.GetBasketId(game.AwayOwner)!,
                                                Email = game.AwayOwner
                                            };
                                            var awayTeam = await _teamDayRepository.GetAsync(awayTeamKey);
                                            var homePoint = homeTeam != null ? _teamCalculator.CalculatePoint(homeTeam.ActivePlayers) : Point.Zero;
                                            var awayPoint = awayTeam != null ? _teamCalculator.CalculatePoint(awayTeam.ActivePlayers) : Point.Zero;
                                            game.Result = new GameResult
                                            {
                                                Home = homePoint,
                                                Away = awayPoint,
                                            };
                                            game.Result.CalculateGoals(leagueSettings);
                                        }
                                    }
                                }
                                if (!(liveLeague.Rounds.Count == 1 && liveLeague.Rounds.ContainsKey("Finals")))
                                {
                                    var rank = await _rankRepository.GetAsync(leagueKey).NoContext();
                                    if (rank != null)
                                    {
                                        liveLeague.Rank = _rankCalculator.AddDay(leagueSettings, rank, liveLeague.Rounds, league.Type);
                                    }
                                }
                                liveGroup.Leagues.Add(liveLeague);
                            }
                        }
                        await _liveGroupRepository.UpdateAsync(group.Id, liveGroup).NoContext();
                    }
                    await _logger.LogAsync<LiveJob>($"Live parsati.").NoContext();
                }
            }
        }

        public Task OnException(Exception exception)
            => _logger.LogErrorAsync<LiveVotesJob>(exception);
    }
}

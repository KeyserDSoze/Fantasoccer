using System.Timers;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;

namespace Fantasoccer.Business.BackgroundJobs.GroupsManager
{
    public sealed class GroupsManagerJob : IBackgroundJob
    {
        private readonly IFantasoccerLogger _logger;
        private readonly ITeamCalculator _teamCalculator;
        private readonly IRankCalculator _rankCalculator;
        private readonly IFactory<ILeagueCalculator> _leagueCalculatorFactory;
        private readonly IRepository<Group, string> _groups;
        private readonly IRepository<Calendar, LeagueKey> _calendarRepository;
        private readonly IRepository<Rank, YearDayLeague> _rankDayRepository;
        private readonly IRepository<Rank, LeagueKey> _rankRepository;
        private readonly IRepository<Team, TeamDayKey> _teamDayRepository;
        private readonly IRepository<RealCalendar, int> _serieACalendar;

        public GroupsManagerJob(IFantasoccerLogger logger,
            ITeamCalculator teamCalculator,
            IRankCalculator rankCalculator,
            IFactory<ILeagueCalculator> leagueCalculatorFactory,
            IRepository<Group, string> groups,
            IRepository<Calendar, LeagueKey> calendarRepository,
            IRepository<Rank, YearDayLeague> rankDayRepository,
            IRepository<Rank, LeagueKey> rankRepository,
            IRepository<Team, TeamDayKey> teamDayRepository,
            IRepository<RealCalendar, int> serieACalendar)
        {
            _logger = logger;
            _teamCalculator = teamCalculator;
            _rankCalculator = rankCalculator;
            _leagueCalculatorFactory = leagueCalculatorFactory;
            _groups = groups;
            _calendarRepository = calendarRepository;
            _rankDayRepository = rankDayRepository;
            _rankRepository = rankRepository;
            _teamDayRepository = teamDayRepository;
            _serieACalendar = serieACalendar;
        }

        public async Task ActionToDoAsync(CancellationToken cancellationToken)
        {
            var actualYear = FantacalcioTime.ActualYear;
            var realCalendar = await _serieACalendar.GetAsync(actualYear).NoContext();
            if (realCalendar != null)
            {
                var lastDaySerieA = realCalendar.LastDay?.SerieADay ?? 1;
                await _teamCalculator.BuildRequestAsync(FantacalcioTime.ActualYear, lastDaySerieA, LeagueType.League, LeagueSetting.Default, false, true, false);
                var groups = await _groups.ToListAsEntityAsync();
                foreach (var group in groups)
                {
                    foreach (var league in group.Leagues.Where(x => x.Years.Exists(t => t.Year == actualYear)))
                    {
                        var leagueSettings = group.GetAnnualLeague(league.Id, actualYear)?.Settings ?? LeagueSetting.Default;
                        _teamCalculator.SetLeagueSettings(league.Type, leagueSettings);
                        var leagueCalculator = _leagueCalculatorFactory.Create(league.Type.ToString())!;
                        var leagueKey = new LeagueKey { Group = group.Id, League = league.Id, Year = actualYear };
                        var calendar = await _calendarRepository.GetAsync(leagueKey).NoContext();
                        if (calendar != null)
                        {
                            var rank = await _rankRepository.GetAsync(leagueKey).NoContext();
                            if (rank == null || rank.SerieADay < lastDaySerieA)
                            {
                                foreach (var round in calendar.Rounds.Where(x => x.Value.Any(t => t.SerieADay == lastDaySerieA)))
                                {
                                    var day = round.Value.First(x => x.SerieADay == lastDaySerieA);
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
                                rank = leagueCalculator.GetRank(leagueSettings, calendar);
                                var annualLeague = league.Years.First(x => x.Year == actualYear);
                                var teams = group!.Baskets.SelectMany(x => x.Years).Where(x => x.Year == actualYear).SelectMany(x => x.Teams).ToList();
                                leagueCalculator.AddDaysInLeague(actualYear, annualLeague, teams, rank, calendar);
                                await _calendarRepository.UpdateAsync(leagueKey, calendar);
                                await _rankRepository.UpdateAsync(leagueKey, rank);
                                await _rankDayRepository.UpdateAsync(new YearDayLeague
                                {
                                    Group = group.Id,
                                    League = league.Id,
                                    Year = actualYear,
                                    Day = rank.SerieADay,
                                }, rank);
                            }
                        }
                    }
                }
            }
        }

        public Task OnException(Exception exception)
            => _logger.LogErrorAsync<GroupsManagerJob>(exception);
    }
}

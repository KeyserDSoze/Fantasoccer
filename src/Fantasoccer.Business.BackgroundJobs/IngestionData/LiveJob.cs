using System.Timers;
using Fantasoccer.Business;
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
        public async Task ActionToDoAsync(CancellationToken cancellationToken)
        {
            var actualYear = FantacalcioTime.ActualYear;
            var realCalendar = await _serieACalendar.GetAsync(actualYear, cancellationToken).NoContext();
            if (realCalendar != null)
            {
                var liveDay = realCalendar.LiveDay ?? realCalendar.NextDay;
                if (liveDay != default)
                {
                    await _teamCalculator.BuildRequestAsync(actualYear, liveDay.SerieADay, LeagueType.League, LeagueSetting.Default, false, true, false);
                    foreach (var group in await _groupRepository.ToListAsEntityAsync(cancellationToken: cancellationToken))
                    {
                        if (string.IsNullOrWhiteSpace(group.Id))
                            continue;

                        var liveGroup = new LiveGroup
                        {
                            Name = group.Name ?? group.Id,
                            Leagues = new()
                        };
                        foreach (var league in group.Leagues?.Where(x => x?.Years?.Exists(t => t.Year == actualYear) == true) ?? [])
                        {
                            if (string.IsNullOrWhiteSpace(league.Id))
                                continue;

                            var leagueSettings = group.GetAnnualLeague(league.Id, actualYear)?.Settings ?? LeagueSetting.Default;
                            _teamCalculator.SetLeagueSettings(league.GetAnnualType(actualYear), leagueSettings);
                            var leagueKey = new LeagueKey { Group = group.Id, League = league.Id, Year = actualYear };
                            var calendar = await _calendarRepository.GetAsync(leagueKey, cancellationToken).NoContext();
                            if (calendar != null)
                            {
                                var liveLeague = new LiveLeague
                                {
                                    Id = league.Id,
                                    Name = league.Name ?? league.Id,
                                    Rounds = []
                                };
                                foreach (var round in calendar.Rounds ?? [])
                                {
                                    var day = round.Value?.FirstOrDefault(x => x.SerieADay == liveDay.SerieADay);
                                    if (day != null)
                                    {
                                        liveLeague.Rounds.Add(round.Key, day);
                                        foreach (var game in day.Games ?? [])
                                        {
                                            if (game.Result?.IsCancelled == true)
                                                continue;
                                            if (string.IsNullOrWhiteSpace(game.HomeOwner) || string.IsNullOrWhiteSpace(game.AwayOwner) || group.Baskets == null)
                                                continue;

                                            var homeBasketId = group.GetBasketId(game.HomeOwner, actualYear);
                                            var awayBasketId = group.GetBasketId(game.AwayOwner, actualYear);
                                            if (homeBasketId == null || awayBasketId == null)
                                                continue;

                                            var homeTeamKey = new TeamDayKey
                                            {
                                                Year = actualYear,
                                                Group = group.Id,
                                                Day = day.SerieADay,
                                                BasketId = homeBasketId,
                                                Email = game.HomeOwner
                                            };
                                            var homeTeam = await _teamDayRepository.GetAsync(homeTeamKey, cancellationToken);
                                            var awayTeamKey = new TeamDayKey
                                            {
                                                Year = actualYear,
                                                Group = group.Id,
                                                Day = day.SerieADay,
                                                BasketId = awayBasketId,
                                                Email = game.AwayOwner
                                            };
                                            var awayTeam = await _teamDayRepository.GetAsync(awayTeamKey, cancellationToken);
                                            var homePoint = homeTeam?.Players != null ? _teamCalculator.CalculatePoint(homeTeam.ActivePlayers) : Point.Zero;
                                            var awayPoint = awayTeam?.Players != null ? _teamCalculator.CalculatePoint(awayTeam.ActivePlayers) : Point.Zero;
                                            game.Result = new GameResult
                                            {
                                                Home = homePoint,
                                                Away = awayPoint,
                                            };
                                            game.Result.CalculateGoals(leagueSettings);
                                        }
                                    }
                                }
                                var rank = await _rankRepository.GetAsync(leagueKey, cancellationToken).NoContext();
                                if (rank != null)
                                {
                                    if (!liveLeague.Rounds.ContainsKey(Finals) && CanCalculateRank(rank, liveLeague.Rounds))
                                    {
                                        liveLeague.Rank = _rankCalculator.AddDay(leagueSettings, rank, liveLeague.Rounds);
                                    }
                                    else
                                    {
                                        liveLeague.Rank = rank;
                                    }
                                }
                                liveGroup.Leagues.Add(liveLeague);
                            }
                        }
                        if (await _liveGroupRepository.ExistAsync(group.Id, cancellationToken).NoContext())
                            await _liveGroupRepository.UpdateAsync(group.Id, liveGroup, cancellationToken).NoContext();
                        else
                            await _liveGroupRepository.InsertAsync(group.Id, liveGroup, cancellationToken).NoContext();
                    }
                    await _logger.LogAsync<LiveJob>($"Live parsati.").NoContext();
                }
            }
        }
        private const string Finals = "Finals";
        private static bool CanCalculateRank(Rank rank, Dictionary<string, Day> rounds)
            => rank.RankedTeams != null && rounds.All(round =>
                round.Value?.Games != null &&
                rank.RankedTeams.TryGetValue(round.Key, out var teams) &&
                round.Value.Games.Where(x => x.Result?.IsCancelled != true).All(game =>
                    teams.Any(x => x.Owner == game.HomeOwner) && teams.Any(x => x.Owner == game.AwayOwner)));

        public Task OnException(Exception exception)
            => _logger.LogErrorAsync<LiveJob>(exception);
    }
}

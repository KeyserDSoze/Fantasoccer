using System.Timers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RepositoryFramework;

namespace Fantasoccer.Business.BackgroundJobs.GroupsManager;

public sealed class HallOfFameJob : IBackgroundJob
{
    private readonly ILogger<HallOfFameJob> _logger;
    private readonly IRepository<Group, string> _groups;
    private readonly IRepository<Calendar, LeagueKey> _calendarRepository;
    private readonly IRepository<Rank, LeagueKey> _rankRepository;
    private readonly IRepository<HallOfFame, GeneralLeagueKey> _hallOfFameRepository;
    private readonly IRepository<Team, TeamKey> _teamRepository;
    private readonly IFactory<ILeagueCalculator> _leagueCalculatorFactory;
    private string? _currentRunId;
    private bool _currentRunWasCanceled;

    public HallOfFameJob(
        ILogger<HallOfFameJob> logger,
        IRepository<Group, string> groups,
        IRepository<Calendar, LeagueKey> calendarRepository,
        IRepository<Rank, LeagueKey> rankRepository,
        IRepository<HallOfFame, GeneralLeagueKey> hallOfFameRepository,
        IRepository<Team, TeamKey> teamRepository,
        IFactory<ILeagueCalculator> leagueCalculatorFactory)
    {
        _logger = logger;
        _groups = groups;
        _calendarRepository = calendarRepository;
        _rankRepository = rankRepository;
        _hallOfFameRepository = hallOfFameRepository;
        _teamRepository = teamRepository;
        _leagueCalculatorFactory = leagueCalculatorFactory;
    }

    public async Task ActionToDoAsync(CancellationToken cancellationToken)
    {
        var runId = Guid.NewGuid().ToString("N");
        _currentRunId = runId;
        _currentRunWasCanceled = false;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var actualYear = FantacalcioTime.ActualYear;
        var status = "Failed";
        var summary = new HallOfFameRunSummary();

        _logger.LogInformation(
            "HallOfFame refresh started. RunId {RunId}, current fantasy year {ActualYear}",
            runId,
            actualYear);

        try
        {
            var groups = await _groups.ToListAsEntityAsync(cancellationToken: cancellationToken);
            summary.Groups = groups.Count;
            _logger.LogInformation(
                "HallOfFame refresh loaded {GroupCount} groups. RunId {RunId}",
                groups.Count,
                runId);

            foreach (var group in groups)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var leagues = group.Leagues ?? [];
                _logger.LogInformation(
                    "HallOfFame processing group {GroupId} with {LeagueCount} leagues. RunId {RunId}",
                    group.Id,
                    leagues.Count,
                    runId);

                foreach (var league in leagues)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    summary.Leagues++;
                    _logger.LogInformation(
                        "HallOfFame processing league {LeagueId} in group {GroupId}. RunId {RunId}",
                        league.Id,
                        group.Id,
                        runId);

                    var hallOfFame = new HallOfFame
                    {
                        WinningPlayers = [],
                        WinningTeams = [],
                        Ranks = [],
                        RecordGame = null,
                        RecordPlayer = null,
                        PlayerWithMostPointsInYear = null
                    };

                    foreach (var yearlyLeague in (league.Years ?? []).OrderByDescending(x => x.Year))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        summary.YearsVisited++;
                        _logger.LogInformation(
                            "HallOfFame processing year {Year}. Group {GroupId}, league {LeagueId}, run {RunId}",
                            yearlyLeague.Year,
                            group.Id,
                            league.Id,
                            runId);
                        var key = new LeagueKey
                        {
                            Group = group.Id,
                            League = league.Id,
                            Year = yearlyLeague.Year
                        };

                        var rank = await _rankRepository.GetAsync(key, cancellationToken).NoContext();
                        var calendar = await _calendarRepository.GetAsync(key, cancellationToken).NoContext();

                        if (rank == null && calendar == null)
                        {
                            summary.YearsWithoutData++;
                            _logger.LogWarning(
                                "HallOfFame skipped year {Year}: both rank and calendar are missing. Group {GroupId}, league {LeagueId}, RunId {RunId}",
                                yearlyLeague.Year,
                                group.Id,
                                league.Id,
                                runId);
                            continue;
                        }

                        summary.YearsProcessed++;
                        if (rank == null)
                        {
                            _logger.LogWarning(
                                "HallOfFame rank is missing for year {Year}; calendar records will still be processed. Group {GroupId}, league {LeagueId}, RunId {RunId}",
                                yearlyLeague.Year,
                                group.Id,
                                league.Id,
                                runId);
                        }
                        if (calendar == null)
                        {
                            _logger.LogWarning(
                                "HallOfFame calendar is missing for year {Year}; rank totals will still be processed. Group {GroupId}, league {LeagueId}, RunId {RunId}",
                                yearlyLeague.Year,
                                group.Id,
                                league.Id,
                                runId);
                        }

                        if (rank?.RankedTeams != null)
                            AddRankTotals(hallOfFame, rank);

                        var yearGameSummary = calendar == null
                            ? new CalendarProcessingSummary()
                            : ProcessCalendar(
                                hallOfFame,
                                calendar,
                                yearlyLeague.Year,
                                group.Id,
                                league.Id,
                                runId);

                        summary.GamesVisited += yearGameSummary.GamesVisited;
                        summary.GamesWithResults += yearGameSummary.GamesWithResults;
                        summary.GamesWithoutResults += yearGameSummary.GamesWithoutResults;
                        summary.RecordGameUpdates += yearGameSummary.RecordUpdates;

                        if (rank != null && calendar != null)
                        {
                            var seasonCompleted = yearlyLeague.Year < actualYear || IsSeasonCompleted(calendar);
                            if (seasonCompleted)
                            {
                                await AddYearlyWinnersAsync(
                                    hallOfFame,
                                    group,
                                    league,
                                    yearlyLeague.Year,
                                    rank,
                                    calendar,
                                    runId,
                                    cancellationToken);
                            }
                            else
                            {
                                _logger.LogInformation(
                                    "HallOfFame winner calculation deferred: year {Year} is still in progress. Group {GroupId}, league {LeagueId}, RunId {RunId}",
                                    yearlyLeague.Year,
                                    group.Id,
                                    league.Id,
                                    runId);
                            }
                        }

                        _logger.LogInformation(
                            "HallOfFame year processed. Group {GroupId}, league {LeagueId}, year {Year}, rank available {RankAvailable}, calendar available {CalendarAvailable}, games visited {GamesVisited}, games with results {GamesWithResults}, games without results {GamesWithoutResults}, run {RunId}",
                            group.Id,
                            league.Id,
                            yearlyLeague.Year,
                            rank != null,
                            calendar != null,
                            yearGameSummary.GamesVisited,
                            yearGameSummary.GamesWithResults,
                            yearGameSummary.GamesWithoutResults,
                            runId);
                    }

                    await SaveHallOfFameAsync(group.Id, league.Id, hallOfFame, cancellationToken);
                    summary.LeaguesSaved++;
                    summary.WinningTeams += hallOfFame.WinningTeams.Count;
                    summary.WinningPlayers += hallOfFame.WinningPlayers.Count;
                    summary.RankedTeams += hallOfFame.Ranks.Count;

                    var recordGame = hallOfFame.RecordGame;
                    _logger.LogInformation(
                        "HallOfFame league saved. Group {GroupId}, league {LeagueId}, rank entries {RankCount}, winning teams {WinningTeamCount}, winning players {WinningPlayerCount}, record year {RecordYear}, record game {RecordGameId}, record highest score {RecordScore}, run {RunId}",
                        group.Id,
                        league.Id,
                        hallOfFame.Ranks.Count,
                        hallOfFame.WinningTeams.Count,
                        hallOfFame.WinningPlayers.Count,
                        recordGame?.Year,
                        recordGame?.Game.Id,
                        GetRecordGamePoints(recordGame),
                        runId);
                }
            }

            status = "Succeeded";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            status = "Canceled";
            _currentRunWasCanceled = true;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogInformation(
                "HallOfFame refresh finished. Status {Status}, RunId {RunId}, groups {GroupCount}, leagues visited {LeagueCount}, leagues saved {LeagueCountSaved}, years visited {YearCount}, years processed {YearCountProcessed}, years without data {YearCountWithoutData}, games visited {GameCount}, games with results {GameCountWithResults}, games without results {GameCountWithoutResults}, record improvements {RecordGameUpdates}, rank entries {RankedTeamCount}, winning teams {WinningTeamCount}, winning players {WinningPlayerCount}, elapsed milliseconds {ElapsedMilliseconds}",
                status,
                runId,
                summary.Groups,
                summary.Leagues,
                summary.LeaguesSaved,
                summary.YearsVisited,
                summary.YearsProcessed,
                summary.YearsWithoutData,
                summary.GamesVisited,
                summary.GamesWithResults,
                summary.GamesWithoutResults,
                summary.RecordGameUpdates,
                summary.RankedTeams,
                summary.WinningTeams,
                summary.WinningPlayers,
                stopwatch.ElapsedMilliseconds);
        }
    }

    public Task OnException(Exception exception)
    {
        if (exception is OperationCanceledException && _currentRunWasCanceled)
            return Task.CompletedTask;

        _logger.LogError(
            exception,
            "HallOfFame background job failed. RunId {RunId}; use preceding progress events to identify the group, league, and year being processed.",
            _currentRunId);
        return Task.CompletedTask;
    }

    private void AddRankTotals(HallOfFame hallOfFame, Rank rank)
    {
        foreach (var round in rank.RankedTeams)
        {
            foreach (var team in round.Value ?? [])
            {
                var rankedTeam = hallOfFame.Ranks.FirstOrDefault(x => x.Owner == team.Owner);
                if (rankedTeam == null)
                {
                    rankedTeam = new RankedTeam
                    {
                        Owner = team.Owner,
                        Name = team.Name,
                    };
                    hallOfFame.Ranks.Add(rankedTeam);
                }

                rankedTeam.Defeats += team.Defeats;
                rankedTeam.Draws += team.Draws;
                rankedTeam.Goal += team.Goal;
                rankedTeam.Money += team.Money;
                rankedTeam.PlusMoney += team.PlusMoney;
                rankedTeam.Point += team.Point;
                rankedTeam.SufferedGoal += team.SufferedGoal;
                rankedTeam.SufferedValuePoint += team.SufferedValuePoint;
                rankedTeam.ValuePoint += team.ValuePoint;
                rankedTeam.Victories += team.Victories;
            }
        }
    }

    private CalendarProcessingSummary ProcessCalendar(
        HallOfFame hallOfFame,
        Calendar calendar,
        int year,
        string groupId,
        string leagueId,
        string runId)
    {
        var summary = new CalendarProcessingSummary();
        var currentRecordScore = GetRecordGamePoints(hallOfFame.RecordGame);

        foreach (var day in calendar.Rounds?.Values.SelectMany(x => x ?? []) ?? [])
        {
            foreach (var game in day.Games ?? [])
            {
                summary.GamesVisited++;
                var result = game.Result;
                if (result == null || !result.HasValue)
                {
                    summary.GamesWithoutResults++;
                    continue;
                }

                summary.GamesWithResults++;
                var highestScore = MathF.Max(result.Home.Value, result.Away.Value);
                if (hallOfFame.RecordGame != null && highestScore <= currentRecordScore)
                    continue;

                hallOfFame.RecordGame = new RecordGame
                {
                    Game = game,
                    Year = year
                };
                currentRecordScore = highestScore;
                summary.RecordUpdates++;

                _logger.LogInformation(
                    "HallOfFame Partita della Vita record updated. Group {GroupId}, league {LeagueId}, year {Year}, Serie A day {SerieADay}, game {GameId}, home score {HomeScore}, away score {AwayScore}, highest team score {HighestScore}, run {RunId}",
                    groupId,
                    leagueId,
                    year,
                    day.SerieADay,
                    game.Id,
                    result.Home.Value,
                    result.Away.Value,
                    highestScore,
                    runId);
            }
        }

        return summary;
    }

    private async Task AddYearlyWinnersAsync(
        HallOfFame hallOfFame,
        Group group,
        League league,
        int year,
        Rank rank,
        Calendar calendar,
        string runId,
        CancellationToken cancellationToken)
    {
        var calculator = _leagueCalculatorFactory.Create(league.GetAnnualType(year).ToString());
        if (calculator == null)
        {
            _logger.LogWarning(
                "HallOfFame winner calculation skipped: no league calculator for year {Year}, group {GroupId}, league {LeagueId}, run {RunId}",
                year,
                group.Id,
                league.Id,
                runId);
            return;
        }

        var winners = calculator.GetTheYearlyWinners(rank, calendar);
        foreach (var officialWinner in winners)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var annualTeam = (group.Baskets ?? [])
                .Where(basket => basket.Years?.Any(item => item.Year == year) == true)
                .SelectMany(basket => basket.Years!.Where(item => item.Year == year).SelectMany(item => item.Teams ?? []))
                .FirstOrDefault(team => team.Owner == officialWinner.Owner);

            if (annualTeam == null)
            {
                _logger.LogWarning(
                    "HallOfFame winner has no team in the group roster. Group {GroupId}, league {LeagueId}, year {Year}, owner {Owner}, run {RunId}",
                    group.Id,
                    league.Id,
                    year,
                    officialWinner.Owner,
                    runId);
            }

            var winner = hallOfFame.WinningTeams.FirstOrDefault(x => x.Owner == officialWinner.Owner);
            if (winner == null)
            {
                winner = new WinningTeam
                {
                    Owner = officialWinner.Owner,
                    Team = annualTeam?.Name ?? officialWinner.Owner,
                    Wins = new()
                };
                hallOfFame.WinningTeams.Add(winner);
            }

            if (!winner.Wins.ContainsKey(officialWinner.Round))
                winner.Wins.Add(officialWinner.Round, []);
            if (!winner.Wins[officialWinner.Round].Contains(year))
                winner.Wins[officialWinner.Round].Add(year);

            if (annualTeam == null)
                continue;

            var basket = (group.Baskets ?? []).FirstOrDefault(item =>
                item.Years?.Any(annual => annual.Year == year && annual.Teams?.Any(team => team.Owner == officialWinner.Owner) == true) == true);
            if (basket == null)
                continue;

            var team = await _teamRepository.GetAsync(
                new TeamKey { Group = group.Id, Email = officialWinner.Owner, Year = year, BasketId = basket.Id },
                cancellationToken).NoContext();
            if (team == null)
            {
                _logger.LogWarning(
                    "HallOfFame winning team data is missing. Group {GroupId}, league {LeagueId}, year {Year}, run {RunId}",
                    group.Id,
                    league.Id,
                    year,
                    runId);
                continue;
            }

            foreach (var player in team.ActivePlayers ?? [])
            {
                var winningPlayer = hallOfFame.WinningPlayers.FirstOrDefault(x => x.Player.Key == player.Key);
                if (winningPlayer == null)
                {
                    winningPlayer = new WinningPlayer
                    {
                        Player = player,
                        Wins = new()
                    };
                    hallOfFame.WinningPlayers.Add(winningPlayer);
                }

                if (!winningPlayer.Wins.ContainsKey(officialWinner.Round))
                    winningPlayer.Wins.Add(officialWinner.Round, []);
                if (!winningPlayer.Wins[officialWinner.Round].Contains(year))
                    winningPlayer.Wins[officialWinner.Round].Add(year);
            }
        }
    }

    private async Task SaveHallOfFameAsync(
        string groupId,
        string leagueId,
        HallOfFame hallOfFame,
        CancellationToken cancellationToken)
    {
        var key = new GeneralLeagueKey { Group = groupId, League = leagueId };
        if (await _hallOfFameRepository.ExistAsync(key, cancellationToken).NoContext())
            await _hallOfFameRepository.UpdateAsync(key, hallOfFame, cancellationToken).NoContext();
        else
            await _hallOfFameRepository.InsertAsync(key, hallOfFame, cancellationToken).NoContext();
    }

    private static bool IsSeasonCompleted(Calendar calendar)
    {
        var latestDay = calendar.Rounds?.Values
            .SelectMany(x => x ?? [])
            .OrderByDescending(day => day.Number)
            .FirstOrDefault();

        return latestDay?.Games is { Count: > 0 } games
            && games.All(game => game.Result?.HasValue == true);
    }

    private static float GetRecordGamePoints(RecordGame? recordGame)
    {
        var result = recordGame?.Game?.Result;
        return result == null
            ? float.NegativeInfinity
            : MathF.Max(result.Home.Value, result.Away.Value);
    }

    private sealed class HallOfFameRunSummary
    {
        public int Groups { get; set; }
        public int Leagues { get; set; }
        public int LeaguesSaved { get; set; }
        public int YearsVisited { get; set; }
        public int YearsProcessed { get; set; }
        public int YearsWithoutData { get; set; }
        public int GamesVisited { get; set; }
        public int GamesWithResults { get; set; }
        public int GamesWithoutResults { get; set; }
        public int RecordGameUpdates { get; set; }
        public int RankedTeams { get; set; }
        public int WinningTeams { get; set; }
        public int WinningPlayers { get; set; }
    }

    private sealed class CalendarProcessingSummary
    {
        public int GamesVisited { get; set; }
        public int GamesWithResults { get; set; }
        public int GamesWithoutResults { get; set; }
        public int RecordUpdates { get; set; }
    }
}

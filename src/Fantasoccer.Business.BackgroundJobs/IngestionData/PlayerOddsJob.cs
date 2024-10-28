using System.Timers;
using Fantasoccer.Business.IngestionData;
using Fantasoccer.Domain;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;

namespace Fantasoccer.Engine.IngestionData
{
    public class PlayerOddsJob : IBackgroundJob
    {
        private readonly List<IFormationParser> _parsers;
        private readonly IFantasoccerLogger _logger;
        private readonly IRepository<ChancedRealPlayerWrapper, YearDayKey> _repository;
        private readonly IRepository<RealCalendar, int> _calendar;
        private readonly IRepository<RealPlayersWrapper, int> _realPlayersRepository;
        private readonly IRepository<StatPlayersWrapper, int> _statsRepository;

        public PlayerOddsJob(IFantasoccerLogger logger,
            IFactory<IFormationParser> formations,
            IRepository<ChancedRealPlayerWrapper, YearDayKey> repository,
            IRepository<RealCalendar, int> calendar,
            IRepository<RealPlayersWrapper, int> realPlayersRepository,
            IRepository<StatPlayersWrapper, int> statsRepository)
        {
            _logger = logger;
            _repository = repository;
            _calendar = calendar;
            _parsers =
            [
                formations.Create("Fantagazzetta")!,
                formations.Create("Gazzetta")!,
                //formations.Create("Mediaset")!,
                //formations.Create("Sky")!,
                formations.Create("Injury")!,
            ];
            _realPlayersRepository = realPlayersRepository;
            _statsRepository = statsRepository;
        }
        public async Task ActionToDoAsync(CancellationToken cancellationToken)
        {
            var calendar = await _calendar.GetAsync(FantacalcioTime.ActualYear).NoContext();
            if (calendar != null)
            {
                var day = (calendar.LiveDay ?? calendar.NextDay)?.SerieADay ?? 0;
                if (day > 0)
                {
                    var statPlayersWrapper = await _statsRepository.GetAsync(FantacalcioTime.ActualYear, cancellationToken);
                    var key = new YearDayKey
                    {
                        Year = FantacalcioTime.ActualYear,
                        Day = day
                    };
                    var players = (await _repository.GetAsync(key).NoContext())?.Players ?? [];
                    if (players.Count == 0)
                    {
                        foreach (var player in (await _realPlayersRepository.GetAsync(FantacalcioTime.ActualYear).NoContext())!.Players)
                        {
                            players.Add(new ChancedRealPlayer
                            {
                                Name = player.Name,
                                RealTeam = player.RealTeam,
                                Role = player.Role,
                                Visible = player.Visible,
                                IsActive = player.IsActive,
                                Chance = new Chance()
                            });
                        }
                    }
                    foreach (var player in players.Where(x => x.Chance.LastGame == default))
                    {
                        var currentStatPlayer = statPlayersWrapper!.Players.FirstOrDefault(x => x.Key == player.Key);
                        if (player.Chance == default)
                            player.Chance = new Chance();
                        player.Chance.LastGame = currentStatPlayer?.AllGames?.OrderByDescending(x => x.SerieADay).FirstOrDefault();
                        var positivenessCount = 2;
                        var lastThreeGames = currentStatPlayer?.AllGames?.OrderByDescending(x => x.SerieADay).Take(3);
                        if (lastThreeGames != null)
                            foreach (var theGame in lastThreeGames)
                            {
                                if (theGame.Positiveness > 1)
                                    positivenessCount++;
                                else if (theGame.Positiveness < 0)
                                    positivenessCount--;
                            }
                        if (positivenessCount > 4)
                            positivenessCount = 4;
                        else if (positivenessCount < 0)
                            positivenessCount = 0;
                        player.Chance.Trend = (TrendType)positivenessCount;
                    }
                    foreach (var player in players)
                    {
                        player.Chance.Fantagazzetta = false;
                        player.Chance.Gazzetta = false;
                        player.Chance.Mediaset = false;
                        player.Chance.Sky = false;
                    }
                    foreach (var parser in _parsers)
                    {
                        var playersFromParser = await parser.GetPlayersAsync().NoContext();

                        foreach (var parsedPlayer in playersFromParser)
                        {
                            var chancedPlayer = players.FirstOrDefault(x => x.Key == parsedPlayer.Key);
                            if (chancedPlayer != default)
                            {
                                chancedPlayer.Chance.Fantagazzetta |= parsedPlayer.Chance.Fantagazzetta;
                                chancedPlayer.Chance.Gazzetta |= parsedPlayer.Chance.Gazzetta;
                                chancedPlayer.Chance.Mediaset |= parsedPlayer.Chance.Mediaset;
                                chancedPlayer.Chance.Sky |= parsedPlayer.Chance.Sky;
                                chancedPlayer.Chance.Status = parsedPlayer.Chance.Status;
                                chancedPlayer.Chance.Description = parsedPlayer.Chance.Description;
                            }
                        }
                    }
                    if (await _repository.ExistAsync(key))
                    {
                        await _repository.UpdateAsync(key, new ChancedRealPlayerWrapper { Players = players }).NoContext();
                    }
                    else
                    {
                        await _repository.InsertAsync(key, new ChancedRealPlayerWrapper { Players = players }).NoContext();
                    }
                    await _logger.LogAsync<PlayerOddsJob>($"Players {players.Count} parsate.").NoContext();
                }
                else
                    await _logger.LogAsync<PlayerOddsJob>($"Day è zero").NoContext();
            }
            else
                await _logger.LogAsync<PlayerOddsJob>($"Il calendario è vuoto").NoContext();
        }

        public Task OnException(Exception exception)
            => _logger.LogErrorAsync<PlayerOddsJob>(exception);
    }
}

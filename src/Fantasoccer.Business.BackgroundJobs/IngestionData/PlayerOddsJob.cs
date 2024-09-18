using System.Timers;
using Fantasoccer.Business.IngestionData;
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

        public PlayerOddsJob(IFantasoccerLogger logger,
            IFactory<IFormationParser> formations,
            IRepository<ChancedRealPlayerWrapper, YearDayKey> repository,
            IRepository<RealCalendar, int> calendar,
            IRepository<RealPlayersWrapper, int> realPlayersRepository)
        {
            _logger = logger;
            _repository = repository;
            _calendar = calendar;
            _parsers = new List<IFormationParser>()
            {
                formations.Create("Fantagazzetta"),
                formations.Create("Gazzetta"),
                formations.Create("Mediaset"),
                //formations.Create("Sky"),
                formations.Create("Injury"),
            };
            _realPlayersRepository = realPlayersRepository;
        }
        public async Task ActionToDoAsync(CancellationToken cancellationToken)
        {
            var calendar = await _calendar.GetAsync(FantacalcioTime.ActualYear).NoContext();
            if (calendar != null)
            {
                var day = (calendar.LiveDay ?? calendar.NextDay)?.SerieADay ?? 0;
                if (day > 0)
                {
                    var key = new YearDayKey
                    {
                        Year = FantacalcioTime.ActualYear,
                        Day = day
                    };
                    var players = (await _repository.GetAsync(key).NoContext())?.Players ?? new();
                    if (players.Count == 0)
                    {
                        foreach (var player in (await _realPlayersRepository.GetAsync(FantacalcioTime.ActualYear).NoContext()).Players)
                        {
                            players.Add(new ChancedRealPlayer
                            {
                                Name = player.Name,
                                RealTeam = player.RealTeam,
                                Role = player.Role,
                                Visible = player.Visible,
                                IsActive = player.IsActive,
                                Chance = new Chance
                                {

                                }
                            });
                        }
                    }
                    foreach (var parser in _parsers)
                    {
                        var playersFromParser = await parser.GetPlayersAsync().NoContext();
                        if (players.Count == 0)
                            players = playersFromParser;
                        else
                        {
                            foreach (var parsedPlayer in playersFromParser)
                            {
                                var chancedPlayer = players.FirstOrDefault(x => x.Key == parsedPlayer.Key);
                                if (chancedPlayer == default)
                                    players.Add(parsedPlayer);
                                else
                                    chancedPlayer.Chance = new Chance
                                    {
                                        Fantagazzetta = chancedPlayer.Chance.Fantagazzetta || parsedPlayer.Chance.Fantagazzetta,
                                        Gazzetta = chancedPlayer.Chance.Gazzetta || parsedPlayer.Chance.Gazzetta,
                                        Mediaset = chancedPlayer.Chance.Mediaset || parsedPlayer.Chance.Mediaset,
                                        Sky = chancedPlayer.Chance.Sky || parsedPlayer.Chance.Sky,
                                        Status = chancedPlayer.Chance.Status == ChanceType.Normal ? parsedPlayer.Chance.Status : chancedPlayer.Chance.Status,
                                        Description = parsedPlayer.Chance.Description ?? chancedPlayer.Chance.Description
                                    };
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

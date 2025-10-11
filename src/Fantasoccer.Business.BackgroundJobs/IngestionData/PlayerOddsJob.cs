using System.Globalization;
using System.Net;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
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
                            if (chancedPlayer == default)
                            {
                                chancedPlayer = players.FirstOrDefault(x => IsThisPlayer(parsedPlayer, x));
                            }
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
        private static bool IsThisPlayer(ChancedRealPlayer parsed, ChancedRealPlayer player)
        {
            var parsedPlayerName = NormalizeName(parsed.Name.Split(' ').OrderByDescending(x => x.Length).First().Split('.').OrderByDescending(x => x.Length).First().ToLower()).ToLower();
            var playerName = NormalizeName(player.Name.Split(' ').OrderByDescending(x => x.Length).First().Split('.').OrderByDescending(x => x.Length).First().ToLower()).ToLower();
            var parsedTeamName = NormalizeName(parsed.RealTeam.Name).ToLower();
            var teamName = NormalizeName(player.RealTeam.Name).ToLower();
            if (parsedPlayerName.Contains(playerName) || playerName.Contains(parsedPlayerName))
            {
                return teamName == parsedTeamName;
            }
            return false;
        }
        private static string NormalizeName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            // 1️⃣ Decodifica eventuali entità HTML (es: &#39; -> ')
            var decoded = WebUtility.HtmlDecode(input);

            // 2️⃣ Normalizza in forma decomposed (NFD) per separare lettere e accenti
            var normalized = decoded.Normalize(NormalizationForm.FormD);

            // 3️⃣ Rimuove i segni diacritici (accenti)
            var sb = new StringBuilder();
            foreach (var c in normalized)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }

            var result = sb.ToString();

            // 4️⃣ Rimuove apostrofi, virgolette e altri segni simili
            result = Regex.Replace(result, @"['’`´]", "");

            // 5️⃣ Normalizza spazi multipli
            result = Regex.Replace(result, @"\s+", " ").Trim();

            return result;
        }
        public Task OnException(Exception exception)
            => _logger.LogErrorAsync<PlayerOddsJob>(exception);
    }
}

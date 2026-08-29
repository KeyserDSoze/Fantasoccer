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

        public PlayerOddsJob(IFantasoccerLogger logger,
            IFactory<IFormationParser> formations,
            IRepository<ChancedRealPlayerWrapper, YearDayKey> repository,
            IRepository<RealCalendar, int> calendar,
            IRepository<RealPlayersWrapper, int> realPlayersRepository)
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
        }
        public async Task ActionToDoAsync(CancellationToken cancellationToken)
        {
            var calendar = await _calendar.GetAsync(FantacalcioTime.ActualYear, cancellationToken).NoContext();
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
                    var players = (await _repository.GetAsync(key, cancellationToken).NoContext())?.Players ?? [];
                    if (players.Count == 0)
                    {
                        var realPlayers = await _realPlayersRepository.GetAsync(FantacalcioTime.ActualYear, cancellationToken).NoContext();
                        if (realPlayers?.Players == null || realPlayers.Players.Count == 0)
                        {
                            await _logger.LogAsync<PlayerOddsJob>($"Giocatori reali non disponibili.").NoContext();
                            return;
                        }

                        foreach (var player in realPlayers.Players)
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
                    foreach (var player in players)
                    {
                        player.Chance ??= Chance.Default();
                        player.Chance.Fantagazzetta = false;
                        player.Chance.Gazzetta = false;
                        player.Chance.Mediaset = false;
                        player.Chance.Sky = false;
                        player.Chance.Status = ChanceType.Normal;
                        player.Chance.Description = null;
                    }
                    foreach (var parser in _parsers)
                    {
                        List<ChancedRealPlayer> playersFromParser;
                        try
                        {
                            playersFromParser = await parser.GetPlayersAsync().NoContext() ?? [];
                        }
                        catch (Exception exception)
                        {
                            await _logger.LogErrorAsync<PlayerOddsJob>(exception).NoContext();
                            continue;
                        }

                        foreach (var parsedPlayer in playersFromParser)
                        {
                            if (parsedPlayer.Chance == null || string.IsNullOrWhiteSpace(parsedPlayer.Name) || parsedPlayer.RealTeam == null)
                                continue;

                            var chancedPlayer = players.FirstOrDefault(x => x.Key == parsedPlayer.Key);
                            if (chancedPlayer == default)
                            {
                                chancedPlayer = players.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Name) && x.RealTeam != null && IsThisPlayer(parsedPlayer, x));
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
                    if (await _repository.ExistAsync(key, cancellationToken))
                    {
                        await _repository.UpdateAsync(key, new ChancedRealPlayerWrapper { Players = players }, cancellationToken).NoContext();
                    }
                    else
                    {
                        await _repository.InsertAsync(key, new ChancedRealPlayerWrapper { Players = players }, cancellationToken).NoContext();
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

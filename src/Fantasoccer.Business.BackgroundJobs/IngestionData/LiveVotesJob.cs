using Fantasoccer;
using ProtoBuf;
using System.Text;
using System.Text.RegularExpressions;
using System.Timers;

namespace FantacalcioV4.Engine.IngestionData
{
    public class LiveVotesJob : IBackgroundJob
    {
        private readonly IRepository<FantacalcioLog, string> _logger;
        private readonly Manager Manager;
        private readonly IHttpClientFactory HttpClientFactory;
        private readonly HttpClient HttpClient;
        public LiveVotesJob( IRepository<FantacalcioLog, string> logger, Manager manager, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            Manager = manager;
            HttpClientFactory = httpClientFactory;
            HttpClient = httpClientFactory.CreateClient("base");
        }
        private const string FantacalcioLiveUri = "https://fantacalcio.xyz/Home/Live";
        private const string FantacalcioGameUri = "https://fantacalcio.xyz/Home/Game?id={0}";

        public async Task ActionToDoAsync()
        {
            int actualYear = FantacalcioTime.ActualYear;
            var realCalendar = await new RealCalendarKey().InstanceAsync().NoContext();
            if (realCalendar == default)
            {
                await new SerieACalendarJob(Logger, HttpClientFactory).ActionToDoAsync().NoContext();
                realCalendar = await new RealCalendarKey().InstanceAsync().NoContext();
            }
            var liveDay = realCalendar.LiveDay;
            if (liveDay != default && realCalendar.IsLive)
            {
                List<(Player Player, List<VoteEvent> Events)> events = new();
                var liveVotes = new LiveVotesContext(actualYear, liveDay.SerieADay);
                var currentParsedPlayers = await liveVotes.GetAsync().NoContext() ?? new List<Player>();
                var protoData = await GetLiveAsync(actualYear + 6, realCalendar.LiveDay.SerieADay);
                foreach (var game in protoData.ProtoData)
                {
                    if (game.PlayersHome != null)
                        foreach (var player in game.PlayersHome)
                            SetPlayer(player);
                    if (game.PlayersAway != null)
                        foreach (var player in game.PlayersAway)
                            SetPlayer(player);
                }

                void SetPlayer(PlayerProto playerProto)
                {
                    if (playerProto.Position == "ALL")
                        return;
                    Player player = new(FantacalcioId.GetPlayerKey(actualYear, playerProto.Name.Substring(0, 1).ToUpper() + playerProto.Name[1..].ToLower()), 0, default);
                    Dictionary<int, int> currentEvents = new();
                    if (playerProto.Events != null)
                        foreach (var theEvent in playerProto.Events)
                        {
                            if (!currentEvents.ContainsKey(theEvent))
                                currentEvents.Add(theEvent, 0);
                            currentEvents[theEvent]++;
                        }
                    var role = playerProto.Position.ToLower() switch
                    {
                        "p" => Role.GoalKeeper,
                        "d" => Role.Defensor,
                        "c" => Role.Midfielder,
                        "a" => Role.Forward,
                        _ => Role.Undefined
                    };
                    float vote = (float)playerProto.Vote;
                    player.Vote = new Vote(role,
                        vote == 55f ? 0 : vote,
                        false,
                        currentEvents.ContainsKey(3) ? currentEvents[3] : 0,
                        currentEvents.ContainsKey(9) ? currentEvents[9] : 0,
                        (currentEvents.ContainsKey(5) ? currentEvents[5] : 0) + (currentEvents.ContainsKey(6) ? currentEvents[6] : 0) +
                        (currentEvents.ContainsKey(24) ? currentEvents[24] : 0) + (currentEvents.ContainsKey(21) ? currentEvents[21] : 0) + (currentEvents.ContainsKey(22) ? currentEvents[22] : 0) +
                        (currentEvents.ContainsKey(23) ? currentEvents[23] : 0),
                        currentEvents.ContainsKey(7) ? currentEvents[7] : 0,
                        currentEvents.ContainsKey(4) ? currentEvents[4] : 0,
                        currentEvents.ContainsKey(8) ? currentEvents[8] : 0,
                        currentEvents.ContainsKey(10) ? currentEvents[10] : 0,
                        currentEvents.ContainsKey(1) ? Behaviour.YellowCard : (currentEvents.ContainsKey(2) ? Behaviour.RedCard : Behaviour.Nothing),
                        vote != 55f,
                        currentEvents.ContainsKey(14),
                        currentEvents.ContainsKey(15)
                    //16 goal annullato da VAR
                    );
                    var currentPlayer = currentParsedPlayers.FirstOrDefault(x => x.Key == player.Key);
                    if (currentPlayer != default)
                    {
                        events.Add((player, VoteEvent.WhatChange(currentPlayer.Vote, player.Vote)));
                        currentPlayer.Vote = player.Vote;
                    }
                    else
                    {
                        events.Add((player, VoteEvent.WhatChange(Vote.Default(player.Vote.Role), player.Vote)));
                        currentParsedPlayers.Add(player);
                    }
                }
                await liveVotes.SaveAsync(currentParsedPlayers).NoContext();
                await Logger.MessageAsync<LiveVotesJob>($"Players {currentParsedPlayers.Count} parsati.").NoContext();
                foreach (var group in await new StoredGroup().ListAsync().NoContext())
                {
                    foreach (var league in await Manager.Group.GetLeaguesAsync(group.PrimaryKey).NoContext())
                    {
                        var calendar = await Manager.League.GetCalendarAsync(group.PrimaryKey, league.Name, FantacalcioTime.ActualYear).NoContext();
                        foreach (var team in league.Years.FirstOrDefault(x => x.Year == FantacalcioTime.ActualYear).Teams)
                        {
                            var realTeam = await Manager.Group.GetTeamAsync(group.PrimaryKey, team.Username, FantacalcioTime.ActualYear).NoContext();
                            foreach (var player in realTeam.ActivePlayers)
                            {
                                var playerWithEvent = events.FirstOrDefault(x => x.Player.Key == player.Key);
                                if (playerWithEvent != default)
                                {
                                    if (league.IsMain)
                                        foreach (var theEvent in playerWithEvent.Events)
                                            await Manager.Identity.SendNotificationAsync(team.Username,
                                                new Notification(theEvent.Event, string.Format(theEvent.Message, $"{player.Name} - "),
                                                new(FantacalcioLiveUri)))
                                                .NoContext();
                                    //send the notification to the enemy
                                    var game = calendar.Days.FirstOrDefault(x => x.SerieADay == liveDay.SerieADay)?.Games
                                        .SelectMany(x => x.Value)
                                        .FirstOrDefault(x => x.HomeUsername == team.Username || x.AwayUsername == team.Username);
                                    if (game != default)
                                        foreach (var theEvent in playerWithEvent.Events)
                                            await Manager.Identity.SendNotificationAsync(team.Username == game.HomeUsername ? game.AwayUsername : game.HomeUsername,
                                                    new Notification(theEvent.Event,
                                                    string.Format(theEvent.Message, $"L'avversario {team.Name} in {league.Name}: {player.Name} - "),
                                                    new(string.Format(FantacalcioGameUri, game.Id))))
                                                    .NoContext();
                                }
                            }
                        }
                    }
                }
                await Logger.MessageAsync<LiveVotesJob>($"Notification sent.").NoContext();
            }
        }

        public Task OnException(Exception exception)
             => Logger.CatchAsync<LiveVotesJob>(exception);

        private static readonly Regex RegexForSplitting = new Regex("signedUri");
        private async Task<LiveMessage> GetLiveAsync(int year, int day)
        {
            var httpContent = new StringContent($"{{\"resourcesUri\":[\"https://api.fantacalcio.it/v1/st/{year}/matches/live/{day}.dat\"]}}", Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync("https://www.fantacalcio.it/api/v1/SignedUri", httpContent);
            var responseAsString = await response.Content.ReadAsStringAsync();
            var result = RegexForSplitting.Split(responseAsString)[1].Split('"')[2];

            var theResponse = await HttpClient.GetStreamAsync(result);
            var liveMessage = Serializer.Deserialize<LiveMessage>(theResponse);
            return liveMessage;
        }

        [ProtoContract]
        private class LiveMessage
        {
            [ProtoMember(1)]
            public List<ProtoData> ProtoData { get; set; }
        }
        [ProtoContract]
        private class ProtoData
        {
            [ProtoMember(1)]
            public UInt32 Id { get; set; }
            [ProtoMember(2)]
            public UInt32 MatchId { get; set; }
            [ProtoMember(3)]
            public UInt32 TeamIdHome { get; set; }
            [ProtoMember(4)]
            public UInt32 TeamIdAway { get; set; }
            [ProtoMember(5)]
            public UInt32 GoalHome { get; set; }
            [ProtoMember(6)]
            public UInt32 GoalAway { get; set; }
            [ProtoMember(7)]
            public UInt64 FhDate { get; set; }
            [ProtoMember(8)]
            public UInt64 ShDate { get; set; }
            [ProtoMember(9)]
            public UInt64 MatchDate { get; set; }
            [ProtoMember(10)]
            public UInt32 Status { get; set; }
            [ProtoMember(11)]
            public string TrendHome { get; set; }
            [ProtoMember(12)]
            public string TrendAway { get; set; }
            [ProtoMember(13)]
            public string TeamHome { get; set; }
            [ProtoMember(14)]
            public string TeamAway { get; set; }
            [ProtoMember(15)]
            public List<PlayerProto> PlayersHome { get; set; }
            [ProtoMember(16)]
            public List<PlayerProto> PlayersAway { get; set; }
        }
        [ProtoContract]
        private class PlayerProto
        {
            [ProtoMember(1)]
            public UInt32 Id { get; set; }
            [ProtoMember(2)]
            public string Name { get; set; }
            [ProtoMember(3)]
            public string Position { get; set; }
            [ProtoMember(4)]
            public double Vote { get; set; }
            [ProtoMember(5)]
            public List<int> Events { get; set; }
            [ProtoMember(6)]
            public List<int> EventsMinutes { get; set; }
            [ProtoMember(7)]
            public int StampId { get; set; }
            [ProtoMember(8)]
            public UInt32 Index { get; set; }
            [ProtoMember(9)]
            public UInt32 SubstitutionId { get; set; }
        }

    }
}
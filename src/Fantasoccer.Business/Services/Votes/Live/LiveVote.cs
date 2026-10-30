using System.Text;
using System.Text.Json;
using ProtoBuf;

namespace Fantasoccer.Business.IngestionData
{
    internal sealed class LiveVote : ILiveVote
    {
        private readonly HttpClient _httpClient;
        public LiveVote(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("base");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Origin", "https://www.fantacalcio.it");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://www.fantacalcio.it/live-serie-a");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Mode", "cors");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Ch-Ua", "\"Chromium\";v=\"116\", \"Not)A;Brand\";v=\"24\", \"Google Chrome\";v=\"116\"");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Ch-Ua-Mobile", "?0");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Ch-Ua-Platform", "Windows");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Dest", "empty");
        }
        public async Task<List<VotedRealPlayer>> GetAsync(int year, int day)
        {
            var players = new List<VotedRealPlayer>();
            var protoData = await GetLiveAsync(year + 6, day);
            if (protoData != null)
            {
                foreach (var game in protoData.ProtoData ?? [])
                {
                    if (game.PlayersHome != null)
                        foreach (var player in game.PlayersHome)
                            SetPlayer(game.TeamHome, player);
                    if (game.PlayersAway != null)
                        foreach (var player in game.PlayersAway)
                            SetPlayer(game.TeamAway, player);
                }
            }

            void SetPlayer(string teamName, PlayerProto playerProto)
            {
                if (string.IsNullOrWhiteSpace(teamName) || string.IsNullOrWhiteSpace(playerProto.Name) ||
                    string.IsNullOrWhiteSpace(playerProto.Position) || playerProto.Position == "ALL")
                    return;
                VotedRealPlayer player = new()
                {
                    Name = playerProto.Name[..1].ToUpper() + playerProto.Name[1..].ToLower(),
                    RealTeam = new RealTeam
                    {
                        Name = teamName
                    }
                };
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
                player.Role = role;
                player.IsActive = true;
                var vote = (float)playerProto.Vote;
                player.Vote = new Vote
                {
                    Role = role,
                    Value = vote == 55f ? 0 : vote,
                    IsFinal = false,
                    Goal = getValue(3),
                    Penalty = getValue(9),
                    Assist = (currentEvents.ContainsKey(5) ? currentEvents[5] : 0) + (currentEvents.ContainsKey(6) ? currentEvents[6] : 0) +
                            (currentEvents.ContainsKey(24) ? currentEvents[24] : 0) + (currentEvents.ContainsKey(21) ? currentEvents[21] : 0) + (currentEvents.ContainsKey(22) ? currentEvents[22] : 0) +
                            (currentEvents.ContainsKey(23) ? currentEvents[23] : 0),
                    StoppedPenalty = getValue(7),
                    SufferedGoal = getValue(4),
                    WrongedPenalty = getValue(8),
                    OwnGoal = getValue(10),
                    ManOfTheMatch = getValue(26) == 1,
                    Status = currentEvents.ContainsKey(1) ? Behaviour.YellowCard : (currentEvents.ContainsKey(2) ? Behaviour.RedCard : Behaviour.Nothing),
                    HasVote = vote != 55f,
                    IsIn = currentEvents.ContainsKey(15),
                    IsOut = currentEvents.ContainsKey(14),
                };
                int getValue(int x)
                {
                    return currentEvents.ContainsKey(x) ? currentEvents[x] : 0;
                }
                players.Add(player);
            }
            return players;
        }
        private async Task<LiveMessage?> GetLiveAsync(int year, int day)
        {
            var httpContent = new StringContent($"{{\"resourcesUri\":[\"https://api.fantacalcio.it/v1/st/{year}/matches/live/{day}.dat\"]}}", Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("https://www.fantacalcio.it/api/v1/SignedUri", httpContent);
            response.EnsureSuccessStatusCode();
            var responseAsString = await response.Content.ReadAsStringAsync();
            if (responseAsString.Contains("Not Found", StringComparison.OrdinalIgnoreCase))
                return null;

            using var document = JsonDocument.Parse(responseAsString);
            var signedUri = FindSignedUri(document.RootElement);
            if (!Uri.TryCreate(signedUri, UriKind.Absolute, out var uri))
                return null;

            await using var theResponse = await _httpClient.GetStreamAsync(uri);
            return Serializer.Deserialize<LiveMessage>(theResponse);

            static string? FindSignedUri(JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in element.EnumerateObject())
                    {
                        if (property.NameEquals("signedUri") && property.Value.ValueKind == JsonValueKind.String)
                            return property.Value.GetString();
                        var value = FindSignedUri(property.Value);
                        if (value != null)
                            return value;
                    }
                }
                else if (element.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in element.EnumerateArray())
                    {
                        var value = FindSignedUri(item);
                        if (value != null)
                            return value;
                    }
                }
                return null;
            }
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

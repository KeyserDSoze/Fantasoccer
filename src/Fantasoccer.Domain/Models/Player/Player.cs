using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public record Player(string Key, int Price, PlayerInTeamStatus TeamStatus)
    {
        private RealPlayer miniPlayer;
        [JsonIgnore]
        public RealPlayer Value => miniPlayer ??= PlayerValue.Instance(Key);
        [JsonIgnore]
        public string Name => Value?.Name ?? string.Empty;
        [JsonIgnore]
        public string RealTeam => Value?.RealTeam ?? string.Empty;
        public Role Role => Value?.Role ?? Role.Undefined;
        public Position Position { get; set; }
        [JsonIgnore]
        public Position PositionBeforeChange { get; set; }
        public Chance Chance { get; set; }
        public Vote Vote { get; set; }
        public void SetVote(Player player, LeagueSetting settings)
        {
            Vote = player?.Vote ?? Vote.Default(Role);
            Vote.FinalValue(Role, settings);
        }
        [JsonIgnore]
        public Position Mirror
        {
            get
            {
                if (this.Position.Status != FantaSoccerRole.Tribune)
                    return new Position(Position.X, Field.Max * 2 + Field.Span - Position.Y);
                return this.Position;
            }
        }
    }
}
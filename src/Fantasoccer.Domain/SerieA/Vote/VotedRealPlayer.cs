using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class VotedRealPlayer : RealPlayer
    {
        [JsonPropertyName("v")]
        public Vote Vote { get; set; }
        public static VotedRealPlayer Empty { get; } = new VotedRealPlayer()
        {
            IsActive = true,
            Name = string.Empty,
            RealTeam = new RealTeam { Name = string.Empty },
            Role = Role.Undefined,
            Visible = true,
            Vote = Vote.Default(Role.Undefined)
        };
    }
}

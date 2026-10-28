using System.Text.Json.Serialization;

namespace Fantasoccer;

public sealed class PostponementSettings
{
    [JsonPropertyName("w")]
    public bool WaitForRecovery { get; set; }
    [JsonPropertyName("p")]
    public float Goalkeeper { get; set; } = 6;
    [JsonPropertyName("d")]
    public float Defender { get; set; } = 6;
    [JsonPropertyName("c")]
    public float Midfielder { get; set; } = 6;
    [JsonPropertyName("a")]
    public float Forward { get; set; } = 6;

    public float ValueFor(Role role) => role switch
    {
        Role.GoalKeeper => Goalkeeper, Role.Defensor => Defender, Role.Midfielder => Midfielder, Role.Forward => Forward, _ => 6
    };
}

using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public record VoteValue
    {
        [JsonPropertyName("v")]
        public float Value { get; set; } = -1000;
        [JsonPropertyName("s")]
        public bool Special { get; set; }
        public static VoteValue Empty => new();
        [JsonIgnore]
        public bool IsEmpty => Value == -1000;
        [JsonIgnore]
        public bool IsZero => Value == 0;
    }
}

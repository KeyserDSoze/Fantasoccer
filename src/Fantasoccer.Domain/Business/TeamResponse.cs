using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public sealed class TeamResponse
    {
        [JsonPropertyName("i")]
        public bool IsOk { get; }
        [JsonPropertyName("m")]
        public string? Message { get; }
        public TeamResponse(bool isOk, string? message = null)
        {
            IsOk = isOk;
            Message = message;
        }
    }
}

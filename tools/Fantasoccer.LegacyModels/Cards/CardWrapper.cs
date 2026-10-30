using System.Text.Json.Serialization;

namespace Fantasoccer
{
    // The card library document of the retired Blob storage.
    public sealed class CardWrapper
    {
        [JsonPropertyName("c")]
        public List<Card> Cards { get; set; } = [];
    }
}

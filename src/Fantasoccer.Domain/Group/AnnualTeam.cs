using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class AnnualTeam
    {
        [JsonPropertyName("n")]
        public string Name { get; set; }
        [JsonPropertyName("o")]
        public string Owner { get; set; }
        [JsonPropertyName("a")]
        public List<string> AdditionalOwners { get; set; }
        public bool IsOwner(string email)
            => email != null && (Owner == email || AdditionalOwners?.Contains(email) == true);
    }
}

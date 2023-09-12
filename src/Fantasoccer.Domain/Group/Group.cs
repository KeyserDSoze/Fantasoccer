using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class Group
    {
        [JsonPropertyName("i")]
        public string Id { get; set; }
        [JsonPropertyName("n")]
        public string Name { get; set; }
        [JsonPropertyName("l")]
        public List<League> Leagues { get; set; }
        [JsonPropertyName("u")]
        public List<UserOfAGroup> Users { get; set; }
        [JsonPropertyName("b")]
        public List<Basket> Baskets { get; set; }
        [JsonIgnore]
        public IEnumerable<UserOfAGroup> ActiveUsers => Users.Where(x => x.Role != IdentityRole.None);
        [JsonIgnore]
        public IEnumerable<UserOfAGroup> RemovedUsers => Users.Where(x => x.Role == IdentityRole.None);
        [JsonIgnore]
        public IEnumerable<UserOfAGroup> AdminUsers => Users.Where(x => x.Role.HasFlag(IdentityRole.Admin));
        [JsonIgnore]
        public IEnumerable<UserOfAGroup> ReaderUsers => Users.Where(x => x.Role.HasFlag(IdentityRole.Reader));
        [JsonIgnore]
        public IEnumerable<UserOfAGroup> Participants => Users.Where(x => x.Role.HasFlag(IdentityRole.Participant));
        public GroupForIdentity ToGroupIdentity(string email)
        {
            var group = new GroupForIdentity
            {
                Id = Id,
                Name = Name,
                Role = Users.FirstOrDefault(x => x.Email == email)?.Role ?? IdentityRole.None,
            };
            return group;
        }
    }
}

using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class Group
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<League> Leagues { get; set; }
        public List<UserOfAGroup> Users { get; set; }
        [JsonIgnore]
        public IEnumerable<UserOfAGroup> ActiveUsers => Users.Where(x => x.Role != IdentityRole.Old);
        [JsonIgnore]
        public IEnumerable<UserOfAGroup> RemovedUsers => Users.Where(x => x.Role == IdentityRole.Old);
        [JsonIgnore]
        public IEnumerable<UserOfAGroup> AdminUsers => Users.Where(x => x.Role.HasFlag(IdentityRole.Admin));
        [JsonIgnore]
        public IEnumerable<UserOfAGroup> ReaderUsers => Users.Where(x => x.Role.HasFlag(IdentityRole.Reader));
        [JsonIgnore]
        public IEnumerable<UserOfAGroup> Participants => Users.Where(x => x.Role.HasFlag(IdentityRole.Participant));

    }
}

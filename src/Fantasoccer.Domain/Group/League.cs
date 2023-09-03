using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class League
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool IsMain { get; set; }
        public LeagueType Type { get; set; }
        public List<AnnualLeague> Years { get; set; }
        public List<UserOfAGroup> Users { get; set; }
        [JsonIgnore]
        public int Participant => Participants.Count();
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

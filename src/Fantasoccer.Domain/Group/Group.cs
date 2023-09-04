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
                Leagues = new(),
                Role = Users.FirstOrDefault(x => x.Email == email)?.Role ?? IdentityRole.None,
                TeamName = Leagues
                    .SelectMany(x => x.Years)
                    .OrderByDescending(x => x.Year)
                    .SelectMany(x => x.Teams)
                    .Where(x => x.Owner == email)
                    .FirstOrDefault()?
                    .Name ?? string.Empty,
            };
            foreach (var league in Leagues.Where(x => x.Years.OrderByDescending(x => x.Year).First().Teams.Any(x => x.Owner == email)))
            {
                group.Leagues.Add(new LeagueForIdentity
                {
                    Id = league.Id,
                    Name = league.Name,
                    Type = league.Type
                });
            }
            return group;
        }
    }
}

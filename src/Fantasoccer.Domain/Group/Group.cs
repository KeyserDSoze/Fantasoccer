using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class Group
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<League> Leagues { get; set; }
        public List<UserOfAGroup> Users { get; set; }
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
                Leagues = new(),
                Role = Users.FirstOrDefault(x => x.Email == email)?.Role ?? IdentityRole.None,
                Teams = new()
            };
            var year = FantacalcioTime.ActualYear;
            foreach (var basketId in Leagues
                    .SelectMany(x => x.Years)
                    .Where(x => x.Year == year)
                    .SelectMany(x => x.BasketsId))
            {
                if (Baskets != null)
                {
                    foreach (var basketsForYear in Baskets)
                    {
                        if (basketsForYear.Years != null)
                        {
                            var basket = basketsForYear.Years.Find(x => x.Year == year);
                            if (basket?.Teams != null)
                            {
                                foreach (var team in basket.Teams.Where(x => x.IsOwner(email)))
                                {
                                    group.Teams.Add(new BasketTeamForIdentity
                                    {
                                        BasketId = basketsForYear.Id,
                                        BasketName = basketsForYear.Name,
                                        Name = team.Name,
                                        Owner = team.Owner
                                    });
                                }
                            }
                        }
                    }
                }
            }
            foreach (var league in Leagues)
            {
                group.Leagues.Add(new LeagueForIdentity
                {
                    Id = league.Id,
                    Name = league.Name,
                    Type = league.Type,
                    BasketsId = league.Years.Find(x => x.Year == year)?.BasketsId ?? new()
                });
            }
            return group;
        }
    }
}

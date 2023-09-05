using System.Text.Json.Serialization;

namespace Fantasoccer.Applications.App
{
    public class FantasoccerState
    {
        public string? Email { get; set; }
        public string? Username { get; set; }
        public string? GroupId { get; set; }
        public string? LeagueId { get; set; }
        public DateTime ExpirationDate { get; set; } = DateTime.UtcNow.AddDays(2);
        [JsonIgnore]
        public LeagueSetting? LeagueSettings => AnnualLeague?.Settings;
        [JsonIgnore]
        public Group? Group { get; set; }
        [JsonIgnore]
        public League? League { get; set; }
        [JsonIgnore]
        public AnnualLeague? AnnualLeague { get; set; }
        [JsonIgnore]
        public bool IsExpired => DateTime.UtcNow > ExpirationDate;
        public int Year { get; set; } = FantacalcioTime.ActualYear;
        [JsonIgnore]
        public Func<ValueTask>? Update { get; set; }
        [JsonIgnore]
        public LeagueKey LeagueKey => new() { Group = GroupId!, League = LeagueId!, Year = Year };
        [JsonIgnore]
        public TeamKey TeamKey => new() { Group = GroupId!, Year = Year, Email = Email! };
        public bool IsAdmin(AppIdentity identity)
        {
            if (identity == null)
                return false;
            return Group?.AdminUsers.Any(x => x.Email == identity.Email) == true;
        }
        public AnnualLeague? GetLeague(Group group)
        {

            var league = group.Leagues.FirstOrDefault(x => x.Id == LeagueId);
            if (league != null)
            {
                var yearLeague = league.Years.FirstOrDefault(x => x.Year == Year);
                return yearLeague;
            }
            return default;
        }
        public TeamKey OtherTeamKey(string email) => new() { Group = GroupId!, Year = Year, Email = email };
        public TeamDayKey TeamDayKey(int day, string? email = null) => new()
        {
            Group = GroupId!,
            Email = email ?? Email!,
            Year = Year,
            Day = day
        };
        public YearDayKey YearDayKey(int day) => new()
        {
            Day = day,
            Year = Year
        };
    }
}

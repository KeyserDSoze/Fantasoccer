using System.Threading.Tasks;

namespace FantacalcioV4.Database
{
    public record TeamContext(string Id, int SerieADay = 0)
    {
        public Task SaveAsync(Team team)
        {
            if (SerieADay == 0)
                return FantacalcioContext.SaveAsync(Id, team);
            else
                return FantacalcioContext.SaveAsync($"{Id}/{SerieADay}", team);
        }

        public Task<Team> GetAsync()
        {
            if (SerieADay == 0)
                return FantacalcioContext.GetAsync<Team>(Id);
            else
                return FantacalcioContext.GetAsync<Team>($"{Id}/{SerieADay}");
        }
        public Task<bool> ExistAsync()
        {
            if (SerieADay == 0)
                return FantacalcioContext.ExistAsync(Id);
            else
                return FantacalcioContext.ExistAsync($"{Id}/{SerieADay}");
        }
        public Task<bool> DeleteAsync()
        {
            if (SerieADay == 0)
                return FantacalcioContext.DeleteAsync(Id);
            else
                return FantacalcioContext.DeleteAsync($"{Id}/{SerieADay}");
        }
    }
}
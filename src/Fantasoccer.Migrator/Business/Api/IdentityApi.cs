using FantacalcioV4.Database;
using Microsoft.AspNetCore.Http;
using Rystem.Text;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FantacalcioV4.ApiManager
{
    public class IdentityApi
    {
        private Logger Logger;
        public IdentityApi(Logger logger)
        {
            Logger = logger;
        }
        public async Task<SecuredIdentity> GetUserAsync(string username, string password)
            => await new StoredIdentity().FirstOrDefaultAsync(x => x.PrimaryKey == username && x.SecondaryKey == password);
        public async Task<SecuredIdentity> GetUserAsync(string username)
            => await new StoredIdentity().FirstOrDefaultAsync(x => x.PrimaryKey == username);
        public async Task ChangeTeamNameAsync(string username, string groupName, int year, string name)
        {
            var identity = await new StoredIdentity().FirstOrDefaultAsync(x => x.PrimaryKey == username);
            var group = identity.Groups.FirstOrDefault(x => x.Name == groupName);
            if (group != default)
            {
                var team = group.Teams?.FirstOrDefault(x => x.Year == year);
                if (team != default)
                {
                    group.Teams.Remove(team);
                    group.Teams.Add(team with { Name = name });
                }
                else
                {
                    group.Teams.Add(new TeamByYear(FantacalcioId.GetTeam(groupName, year, username), name, new()));
                }
            }
            await identity.SaveAsync();
        }
        public async Task SaveTeamImageAsync(string teamId, IFormFile image)
        {
            await new ImageWrapper
            {
                Name = $"teams/{teamId}.png",
            }.SaveAsync(image.OpenReadStream(), image.ContentType);
        }
        public async Task<bool> NewUserAsync(StoredIdentity identity)
        {
            if (!await identity.ExistsAsync())
                return await identity.SaveAsync();
            return false;
        }
        public Task<bool> UpdateUserAsync(StoredIdentity identity)
            => identity.SaveAsync();
    }
}

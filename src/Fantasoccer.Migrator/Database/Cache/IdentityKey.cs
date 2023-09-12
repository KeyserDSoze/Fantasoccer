using FantacalcioV4.Database;
using Rystem.Business;
using System;
using System.Threading.Tasks;

namespace FantacalcioV4
{
    public class IdentityKey : ICacheKey<Identity>
    {
        public string Username { get; set; }
        public async Task<Identity> FetchAsync() 
            => await new StoredIdentity().FirstOrDefaultAsync(x => x.PrimaryKey == Username);
    }
}
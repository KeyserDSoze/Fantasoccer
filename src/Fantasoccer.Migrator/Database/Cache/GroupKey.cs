using Rystem.Business;
using System.Threading.Tasks;
using System;
using FantacalcioV4.Database;

namespace FantacalcioV4
{
    public class GroupKey : ICacheKey<Group>
    {
        public string Name { get; set; }
        public Task<Group> FetchAsync() 
            => new GroupContext(Name).GetAsync();
    }
}

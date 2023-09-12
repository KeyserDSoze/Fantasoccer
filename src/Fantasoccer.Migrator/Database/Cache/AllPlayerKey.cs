using Rystem.Business;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FantacalcioV4.Database
{
    public class AllPlayerKey : ICacheKey<List<RealPlayer>>
    {
        public int Year { get; set; }
        public Task<List<RealPlayer>> FetchAsync()
            => new AllPlayerContext(Year).GetAsync();
    }
}
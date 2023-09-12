using Rystem.Business;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FantacalcioV4.Database
{
    public class LiveKey : ICacheKey<ActualLive>
    {
        public string Group { get; set; }
        public Task<ActualLive> FetchAsync() 
            => default;
    }
}
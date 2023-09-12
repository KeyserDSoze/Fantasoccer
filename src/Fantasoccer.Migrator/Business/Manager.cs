using System.Threading.Tasks;

namespace FantacalcioV4.ApiManager
{
    public record Manager(FantacalcioState State, CoreApi Core, GroupApi Group, LeagueApi League, LiveApi Live, IdentityApi Identity, MarketApi Market, JobApi Job)
    {
       
    }
}
using Fantasoccer.Business.Api.Jobs;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Fantasoccer.Business
{
    public class JobApi
    {
        private readonly GroupApi Group;
        private readonly IHttpClientFactory HttpClientFactory;
        public JobApi(GroupApi group, IHttpClientFactory httpClientFactory)
        {
            Group = group;
            HttpClientFactory = httpClientFactory;
        }

        public Task<List<Player>> SaveFinalVoteAsync(int year, int serieADay, bool forceRefresh)
            => new FinalVote(HttpClientFactory).SaveFinalVoteAsync(year, serieADay, forceRefresh);
        public Task CreateNewYearAndCalendarAsync(string groupName, int year, bool forceDeletion)
            => new NewCalendar(Group).CreateNewYearAndCalendarAsync(groupName, year, forceDeletion);
    }
}

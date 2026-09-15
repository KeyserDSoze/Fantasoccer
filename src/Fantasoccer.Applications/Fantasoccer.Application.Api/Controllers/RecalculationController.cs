using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepositoryFramework;

namespace Fantasoccer.Application.Api.Controllers
{
    [Authorize(Policy = "super_admin")]
    [ApiController]
    [Route("[controller]/[action]")]
    public class RecalculationController : ControllerBase
    {
        private readonly IVotePlayerGenerator _votePlayerGenerator;
        private readonly IStatsPlayerGenerator _statPlayerGenerator;
        private readonly IRepository<RealCalendar, int> _realCalendar;

        public RecalculationController(
            IVotePlayerGenerator votePlayerGenerator,
            IStatsPlayerGenerator statPlayerGenerator,
            IRepository<RealCalendar, int> realCalendar
            )
        {
            _votePlayerGenerator = votePlayerGenerator;
            _statPlayerGenerator = statPlayerGenerator;
            _realCalendar = realCalendar;
        }
        [HttpGet]
        public ValueTask<bool> RecalculateDayAsync([FromQuery] int year, [FromQuery] int serieADay)
            => _votePlayerGenerator.GenerateAndSaveAsync(year, serieADay, true);
        [HttpGet]
        public async ValueTask<bool> RecalculateStatsAsync([FromQuery] int year)
        {
            var realCalendar = await _realCalendar.GetAsync(year);
            if (realCalendar?.LastDay != null)
                await _statPlayerGenerator.GenerateAndSaveAsync(year, realCalendar.LastDay.SerieADay);
            return true;
        }
    }
}

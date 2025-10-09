using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepositoryFramework;
using Rystem.Content;

namespace Fantasoccer.Application.Api.Controllers
{
    [Authorize(Policy = "authenticated")]
    [ApiController]
    [Route("[controller]/[action]")]
    public class SettingsController : ControllerBase
    {
        private readonly IRepository<Group, string> _groups;
        private readonly IRepository<Calendar, LeagueKey> _calendarRepository;
        private readonly IRepository<Rank, YearDayLeague> _dailyRankRepository;
        private readonly IRepository<Rank, LeagueKey> _rankRepository;
        private readonly IRepository<RealCalendar, int> _realCalendarRepository;
        private readonly IRepository<AppIdentity, string> _identityRepository;
        private readonly IRepository<Team, TeamKey> _teamRepository;
        private readonly IRepository<Team, TeamDayKey> _teamDayRepository;
        private readonly IFactory<IContentRepository> _contentFactory;

        public SettingsController(IFactory<IContentRepository> contentFactory,
            IRepository<Team, TeamKey> teamRepository,
            IRepository<Team, TeamDayKey> teamDayRepository,
            IRepository<Group, string> groups,
            IRepository<Calendar, LeagueKey> calendarRepository,
            IRepository<Rank, YearDayLeague> dailyRankRepository,
            IRepository<Rank, LeagueKey> rankRepository,
            IRepository<RealCalendar, int> realCalendarRepository,
            IRepository<AppIdentity, string> identityRepository)
        {
            _groups = groups;
            _calendarRepository = calendarRepository;
            _dailyRankRepository = dailyRankRepository;
            _rankRepository = rankRepository;
            _realCalendarRepository = realCalendarRepository;
            _identityRepository = identityRepository;
            _teamRepository = teamRepository;
            _teamDayRepository = teamDayRepository;
            _contentFactory = contentFactory;
        }
        private bool CheckIsOwner(Group group, int year, string owner, string basketId)
        {
            var email = User.Identity?.Name;
            if (email == null)
                return false;
            if (group == null)
                return false;
            var basket = group.Baskets.First(x => x.Id == basketId);
            var yearlyBasket = basket.Years.First(x => x.Year == year);
            var team = yearlyBasket.Teams.FirstOrDefault(x => x.Owner == owner);
            if (team == null)
                return false;
            return team.Owner == email || team.AdditionalOwners.Contains(email);
        }
        [HttpPost]
        public async ValueTask<bool> UploadLogoAsync([FromBody] IFormFile image, [FromQuery] string groupId, [FromQuery] int year, [FromQuery] string owner, [FromQuery] string basketId)
        {
            var group = await _groups.GetAsync(groupId);
            if (!CheckIsOwner(group!, year, owner, basketId))
            {
                return false;
            }
            var contentRepository = _contentFactory.Create("logos")!;
            var stream = image?.OpenReadStream();
            if (stream != null)
            {
                var logoKey = $"{groupId}/{year}/{owner}.png";
                var data = await stream!.ToArrayAsync();
                var response = await contentRepository.UploadAsync(logoKey, data, new ContentRepositoryOptions
                {
                    HttpHeaders = new()
                    {
                        ContentType = "image/png"
                    }
                });
                return response;
            }
            return false;
        }
        [HttpGet]
        public async IAsyncEnumerable<string> ChangeTeamNameAsync([FromQuery] string groupId, [FromQuery] string owner, [FromQuery] string newName, [FromQuery] string basketId)
        {
            // Enable response buffering to allow streaming
            Response.Headers["Content-Type"] = "text/plain; charset=utf-8";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no";
            
            if (string.IsNullOrWhiteSpace(newName) || newName.Length < 3 || newName.Length > 30)
            {
                yield return "error: Nome non valido.";
                yield break;
            }
            var group = await _groups.GetAsync(groupId);
            var year = FantacalcioTime.ActualYear;
            if (!CheckIsOwner(group!, year, owner, basketId))
            {
                yield return "error: Non sei owner.";
            }
            else
            {
                yield return "Aggiornamento gruppo";
                var realCalendar = await _realCalendarRepository.GetAsync(year);
                var actualDay = realCalendar?.NextDay?.SerieADay ?? 0;
                var allLeagues = group!.Leagues.Where(x => x.BasketsId.Contains(basketId)).ToList();
                var theBasket = group.Baskets.First(x => x.Id == basketId);
                var yearlyBasket = theBasket.Years.First(x => x.Year == year);
                var theTeamFromBasket = yearlyBasket.Teams.First(x => x.Owner == owner);
                if (string.Equals(theTeamFromBasket.Name, newName, StringComparison.OrdinalIgnoreCase))
                {
                    yield return "error: Il nome è lo stesso.";
                    yield break;
                }
                if (yearlyBasket.Teams.Any(x => string.Equals(x.Name, newName, StringComparison.OrdinalIgnoreCase)))
                {
                    yield return "error: Nome già usato.";
                    yield break;
                }
                theTeamFromBasket.Name = newName;
                await _groups.UpdateAsync(groupId, group);
                yield return "Aggiornamento squadra";
                // Update team
                var teamKey = new TeamKey
                {
                    Group = groupId,
                    BasketId = basketId,
                    Email = owner,
                    Year = year
                };
                var theTeam = await _teamRepository.GetAsync(teamKey);
                if (theTeam != null)
                {
                    theTeam.Name = newName;
                    await _teamRepository.UpdateAsync(teamKey, theTeam);
                }
                // Update rank and calendar for each league where the team is in the basket
                foreach (var league in allLeagues)
                {
                    yield return $"Aggiornamento classifica {league.Name}";
                    var leagueKey = new LeagueKey
                    {
                        Group = groupId,
                        Year = year,
                        League = league.Id
                    };
                    var calendar = await _calendarRepository.GetAsync(leagueKey);
                    if (calendar != null)
                        foreach (var day in calendar.Rounds.SelectMany(x => x.Value))
                        {
                            foreach (var game in day.Games)
                            {
                                if (game.HomeOwner == owner)
                                {
                                    game.Home = newName;
                                }
                                else if (game.AwayOwner == owner)
                                {
                                    game.Away = newName;
                                }
                            }
                        }
                    yield return $"Aggiornamento calendario {league.Name}";
                    var rank = await _rankRepository.GetAsync(leagueKey);
                    if (rank == null)
                        continue;
                    var checkIfNeedsUpdate = false;
                    foreach (var rankedTeams in rank.RankedTeams)
                    {
                        foreach (var team in rankedTeams.Value)
                        {
                            if (team.Owner == owner)
                            {
                                team.Name = newName;
                                checkIfNeedsUpdate = true;
                            }
                        }
                    }
                    if (checkIfNeedsUpdate)
                        await _rankRepository.UpdateAsync(leagueKey, rank);
                }
                for (var i = 1; i <= actualDay; i++)
                {
                    // Update daily rank for each league where the team is in the basket
                    foreach (var league in allLeagues)
                    {
                        yield return $"Aggiornamento classifica incrementale giornata {i} - {league.Name}";
                        var yearDayLeague = new YearDayLeague
                        {
                            Year = year,
                            Day = i,
                            League = league.Id
                        };
                        var rank = await _dailyRankRepository.GetAsync(yearDayLeague);
                        if (rank == null)
                            continue;
                        var checkIfNeedsUpdate = false;
                        foreach (var rankedTeams in rank.RankedTeams)
                        {
                            foreach (var team in rankedTeams.Value)
                            {
                                if (team.Owner == owner)
                                {
                                    team.Name = newName;
                                    checkIfNeedsUpdate = true;
                                }
                            }
                        }
                        if (checkIfNeedsUpdate)
                            await _dailyRankRepository.UpdateAsync(yearDayLeague, rank);
                    }
                    yield return $"Aggiornamento squadra giornata {i}";
                    // Update daily team
                    var teamDayKey = new TeamDayKey
                    {
                        Group = groupId,
                        BasketId = basketId,
                        Email = owner,
                        Year = year,
                        Day = i,
                    };
                    var teamDay = await _teamDayRepository.GetAsync(teamDayKey);
                    if (teamDay == null)
                        continue;
                    teamDay.Name = newName;
                    await _teamDayRepository.UpdateAsync(teamDayKey, teamDay);
                }
            }
            yield return "ok";
        }
        [HttpGet]
        public async ValueTask<bool> UpdateUsernameAsync([FromQuery] string newUsername)
        {
            var email = User.Identity?.Name;
            if (email == null)
                return false;
            var identity = await _identityRepository.GetAsync(email);
            if (identity == null)
                return false;
            if (string.Equals(identity.Username, newUsername, StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.IsNullOrWhiteSpace(newUsername) || newUsername.Length < 3 || newUsername.Length > 20)
                return false;
            var regex = new Regex("^[a-zA-Z0-9_]+$");
            if (!regex.IsMatch(newUsername))
                return false;
            identity.Username = newUsername;
            return await _identityRepository.UpdateAsync(email, identity);
        }
    }
}

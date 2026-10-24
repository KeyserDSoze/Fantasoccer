using Fantasoccer.Business.Services;
using Fantasoccer.Business;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepositoryFramework;

namespace Fantasoccer.Application.Api.Controllers
{
    [Authorize(Policy = "authenticated")]
    [ApiController]
    [Route("[controller]/[action]")]
    public sealed class OpeningCompetitionController : ControllerBase
    {
        private readonly IRepository<Group, string> _groups;
        private readonly IRepository<Rank, LeagueKey> _ranks;
        private readonly IRepository<Team, TeamKey> _teams;
        private readonly IRepository<Team, OpeningTeamKey> _openingTeams;
        private readonly IRepository<Team, OpeningTeamDayKey> _openingTeamDays;
        private readonly IRepository<OpeningCompetitionStandings, OpeningCompetitionKey> _standings;
        private readonly ITeamCalculator _teamCalculator;
        private readonly ITeamChecker _teamChecker;

        public OpeningCompetitionController(IRepository<Group, string> groups,
            IRepository<Rank, LeagueKey> ranks, IRepository<Team, TeamKey> teams,
            IRepository<Team, OpeningTeamKey> openingTeams,
            IRepository<Team, OpeningTeamDayKey> openingTeamDays,
            IRepository<OpeningCompetitionStandings, OpeningCompetitionKey> standings,
            ITeamCalculator teamCalculator, ITeamChecker teamChecker)
        {
            _groups = groups;
            _ranks = ranks;
            _teams = teams;
            _openingTeams = openingTeams;
            _openingTeamDays = openingTeamDays;
            _standings = standings;
            _teamCalculator = teamCalculator;
            _teamChecker = teamChecker;
        }

        [HttpGet]
        public async Task<ActionResult<Team>> TeamAsync([FromQuery] string groupId, [FromQuery] string leagueId, [FromQuery] int year, [FromQuery] int day = 1)
        {
            var resolved = await ResolveTeamAsync(groupId, leagueId, year, day);
            return resolved.Team == null ? NotFound() : resolved.Team;
        }

        [HttpPost]
        public async Task<ActionResult<TeamResponse>> SaveTeamAsync([FromQuery] string groupId, [FromQuery] string leagueId, [FromQuery] int year, [FromQuery] int day, [FromBody] Team team)
        {
            var resolved = await ResolveTeamAsync(groupId, leagueId, year, day);
            if (resolved.Team == null)
                return NotFound();
            if (team?.Players == null || !resolved.IsOwner)
                return new TeamResponse(false, "Non sei il proprietario della squadra.");
            var snapshot = await _openingTeams.GetAsync(new OpeningTeamKey
            {
                Group = groupId, League = leagueId, Year = year, BasketId = resolved.BasketId!, Email = resolved.Owner!
            });
            if (snapshot == null) return NotFound();
            var sourcePlayers = snapshot.Players.Select(x => x.Key).ToHashSet();
            var submittedPlayers = team.Players.Select(x => x.Key).ToList();
            if (submittedPlayers.Count != submittedPlayers.Distinct().Count() || submittedPlayers.Any(x => !sourcePlayers.Contains(x)))
                return new TeamResponse(false, "La formazione contiene giocatori non appartenenti alla rosa iniziale.");
            // Never persist the caller's Team document. Only copy formation positions
            // onto the authoritative roster, retaining status, price, revenue and funds.
            var formation = snapshot.Clone();
            foreach (var player in formation.ActivePlayers)
            {
                var submitted = team.Players.FirstOrDefault(x => x.Key == player.Key);
                if (submitted != null) player.Position = submitted.Position;
            }
            var validation = _teamChecker.Validate(formation);
            if (!validation.IsOk)
                return validation;
            var key = new OpeningTeamDayKey { Group = groupId, League = leagueId, Year = year, BasketId = resolved.BasketId!, Email = resolved.Owner!, Day = day };
            if (await _openingTeamDays.GetAsync(key) == null)
                await _openingTeamDays.InsertAsync(key, formation);
            else
                await _openingTeamDays.UpdateAsync(key, formation);
            await PrepareAsync(groupId, leagueId, year);
            return validation;
        }

        private async Task<(Team? Team, string? Owner, string? BasketId, bool IsOwner)> ResolveTeamAsync(string groupId, string leagueId, int year, int day)
        {
            var email = User.Identity?.Name;
            var group = await _groups.GetAsync(groupId);
            var league = group?.Leagues?.FirstOrDefault(x => x.Id == leagueId);
            var annual = group?.GetAnnualLeague(leagueId, year);
            var settings = annual?.Settings?.OpeningCompetition;
            if (email == null || group == null || league == null || annual == null || !league.IsMain || settings?.Enabled != true || day < 1 || day > settings.SerieADays)
                return (null, null, null, false);
            var previousYear = year - 1;
            var member = group.Baskets?.Where(x => league.BasketsId.Contains(x.Id)).SelectMany(x => x.Years?.FirstOrDefault(y => y.Year == previousYear)?.Teams ?? []).FirstOrDefault(x => x.IsOwner(email));
            if (member == null) return (null, null, null, false);
            var basketId = group.GetBasketId(member.Owner, previousYear);
            if (basketId == null) return (null, null, null, false);
            var key = new OpeningTeamKey { Group = groupId, League = leagueId, Year = year, BasketId = basketId, Email = member.Owner };
            var snapshot = await _openingTeams.GetAsync(key);
            var dayTeam = await _openingTeamDays.GetAsync(new OpeningTeamDayKey { Group = groupId, League = leagueId, Year = year, BasketId = basketId, Email = member.Owner, Day = day });
            return (dayTeam ?? snapshot, member.Owner, basketId, true);
        }

        [HttpGet]
        public async Task<ActionResult<OpeningCompetitionStandings>> PrepareAsync(
            [FromQuery] string groupId, [FromQuery] string leagueId, [FromQuery] int year)
        {
            var group = await _groups.GetAsync(groupId);
            var league = group?.Leagues?.FirstOrDefault(x => x.Id == leagueId);
            var annual = group?.GetAnnualLeague(leagueId, year);
            var settings = annual?.Settings?.OpeningCompetition;
            if (group == null || league == null || annual == null || !league.IsMain || settings?.Enabled != true)
                return NotFound();
            if (settings.SerieADays is < 1 or > 39 || settings.Prizes.Any(x => x.Position < 1 || x.Credits < 0) || settings.Prizes.Select(x => x.Position).Distinct().Count() != settings.Prizes.Count)
                return BadRequest("Configurazione del campionato iniziale non valida.");

            var key = new OpeningCompetitionKey { Group = groupId, League = leagueId, Year = year };
            var previousYear = year - 1;
            var members = group.Baskets?.Where(x => league.BasketsId.Contains(x.Id))
                .SelectMany(x => x.Years?.FirstOrDefault(y => y.Year == previousYear)?.Teams ?? [])
                .GroupBy(x => x.Owner, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToList() ?? [];
            foreach (var member in members)
            {
                var basketId = group.GetBasketId(member.Owner, previousYear);
                if (basketId == null)
                    continue;
                var openingKey = new OpeningTeamKey { Group = groupId, League = leagueId, Year = year, BasketId = basketId, Email = member.Owner };
                if (await _openingTeams.GetAsync(openingKey) == null)
                {
                    var source = await _teams.GetAsync(new TeamKey { Group = groupId, Year = previousYear, BasketId = basketId, Email = member.Owner });
                    if (source != null)
                        await _openingTeams.InsertAsync(openingKey, source);
                }
            }
            var scores = members.ToDictionary(x => x.Owner, _ => 0f, StringComparer.OrdinalIgnoreCase);
            var leagueType = annual.Type == LeagueType.Null ? league.Type : annual.Type;
            for (var day = 1; day <= settings.SerieADays; day++)
            {
                await _teamCalculator.BuildRequestAsync(year, day, leagueType, annual.Settings, true, true, true);
                foreach (var member in members)
                {
                    var basketId = group.GetBasketId(member.Owner, previousYear);
                    if (basketId == null)
                        continue;
                    var openingTeam = await _openingTeamDays.GetAsync(new OpeningTeamDayKey { Group = groupId, League = leagueId, Year = year, BasketId = basketId, Email = member.Owner, Day = day })
                        ?? await _openingTeams.GetAsync(new OpeningTeamKey { Group = groupId, League = leagueId, Year = year, BasketId = basketId, Email = member.Owner });
                    if (openingTeam != null)
                        scores[member.Owner] += _teamCalculator.CalculatePoint(openingTeam.ActivePlayers).Value;
                }
            }
            var owners = members.Select(x => (x.Owner, Score: scores[x.Owner])).ToList();
            var prizes = OpeningCompetitionPrizeCalculator.Calculate(
                settings.Prizes.Select(x => new OpeningCompetitionPrizeRule(x.Position, x.Credits)), owners);
            var rankedOwners = owners.Select(x => new { x.Owner, Position = owners.Count(y => y.Score > x.Score) + 1 }).ToDictionary(x => x.Owner, x => x.Position, StringComparer.OrdinalIgnoreCase);
            var result = new OpeningCompetitionStandings
            {
                Group = groupId, League = leagueId, Year = year, SerieADays = settings.SerieADays,
                Teams = members.Select(x => new OpeningCompetitionStanding
                {
                    Owner = x.Owner, Name = x.Name, Score = scores[x.Owner],
                    Prize = prizes.GetValueOrDefault(x.Owner), Position = rankedOwners[x.Owner]
                }).OrderByDescending(x => x.Score).ToList()
            };

            var existing = await _standings.GetAsync(key);
            if (existing == null)
                await _standings.InsertAsync(key, result);
            else
                await _standings.UpdateAsync(key, result);
            return result;
        }
    }
}

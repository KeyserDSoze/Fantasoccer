using Fantasoccer.Business;
using System.Threading.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepositoryFramework;

namespace Fantasoccer.Application.Api.Controllers
{
    [Authorize(Policy = "authenticated")]
    [ApiController]
    [Route("[controller]/[action]")]
    public class GameController : ControllerBase
    {
        private readonly IFactory<ILeagueCalculator> _leagueCalculatorFactory;
        private readonly ITeamCalculator _teamCalculator;
        private readonly IRepository<Calendar, LeagueKey> _calendar;
        private readonly IRepository<RealCalendar, int> _realCalendar;
        private readonly IRepository<Group, string> _groups;
        private readonly IRepository<AppIdentity, string> _users;
        private readonly IRepository<Team, TeamKey> _teamRepository;
        private readonly IRepository<Team, TeamDayKey> _teamDayRepository;
        private readonly ITeamChecker _teamChecker;
        private readonly IFantasoccerLogger _logger;
        private readonly ILock _lock;
        private readonly IOptimalFormationService _optimalFormationService;

        public GameController(IFactory<ILeagueCalculator> leagueCalculatorFactory,
            ITeamCalculator teamCalculator,
            IRepository<Calendar, LeagueKey> calendar,
            IRepository<RealCalendar, int> realCalendar,
            IRepository<Group, string> groups,
            IRepository<Team, TeamKey> teamRepository,
            IRepository<Team, TeamDayKey> teamDayRepository,
            ITeamChecker teamChecker,
            IRepository<AppIdentity, string> users,
            IFantasoccerLogger logger,
            ILock @lock,
            IOptimalFormationService optimalFormationService)
        {
            _leagueCalculatorFactory = leagueCalculatorFactory;
            _teamCalculator = teamCalculator;
            _calendar = calendar;
            _realCalendar = realCalendar;
            _groups = groups;
            _teamRepository = teamRepository;
            _teamDayRepository = teamDayRepository;
            _teamChecker = teamChecker;
            _users = users;
            _logger = logger;
            _lock = @lock;
            _optimalFormationService = optimalFormationService;
        }

        [HttpGet]
        public async ValueTask<ActionResult<OptimalFormationResult>> OptimalFormationAsync(
            [FromQuery] string groupId,
            [FromQuery] string leagueId,
            [FromQuery] int year,
            [FromQuery] int day,
            [FromQuery] string owner,
            [FromQuery] bool? asAdmin = false)
        {
            try
            {
                if (User.Identity?.Name == null)
                    return NotFound();

                var identity = await _users.GetAsync(User.Identity.Name);
                var groupForIdentity = identity?.Groups.FirstOrDefault(x => string.Equals(x.Id, groupId, StringComparison.OrdinalIgnoreCase));
                if (groupForIdentity == null)
                    return NotFound();

                var group = await _groups.GetAsync(groupId);
                var league = group?.Leagues.FirstOrDefault(x => string.Equals(x.Id, leagueId, StringComparison.OrdinalIgnoreCase));
                var annualLeague = league?.Years.FirstOrDefault(x => x.Year == year);
                var basket = group?.Baskets.FirstOrDefault(x =>
                    league?.BasketsId.Contains(x.Id, StringComparer.OrdinalIgnoreCase) == true &&
                    x.Years?.FirstOrDefault(y => y.Year == year)?.Teams?.Any(t => string.Equals(t.Owner, owner, StringComparison.OrdinalIgnoreCase)) == true);
                var annualTeam = basket?.Years.FirstOrDefault(x => x.Year == year)?.Teams
                    .FirstOrDefault(x => string.Equals(x.Owner, owner, StringComparison.OrdinalIgnoreCase));
                if (group == null || league == null || annualLeague == null || basket == null || annualTeam == null)
                    return NotFound();
                if (!annualTeam.IsOwner(User.Identity.Name) &&
                    !(asAdmin == true && groupForIdentity.Role.HasFlag(IdentityRole.SuperAdmin)))
                    return Forbid();

                var team = await _teamDayRepository.GetAsync(new TeamDayKey
                {
                    Group = groupId,
                    BasketId = basket.Id,
                    Email = owner,
                    Year = year,
                    Day = day
                }) ?? await _teamRepository.GetAsync(new TeamKey
                {
                    Group = groupId,
                    BasketId = basket.Id,
                    Email = owner,
                    Year = year
                });
                if (team == null)
                    return NotFound();

                var leagueType = annualLeague.Type == LeagueType.Null
                    ? (league.Type == LeagueType.Null ? LeagueType.League : league.Type)
                    : annualLeague.Type;
                return Ok(await _optimalFormationService.CalculateAsync(
                    team,
                    year,
                    day,
                    leagueType,
                    annualLeague.Settings ?? LeagueSetting.Default,
                    HttpContext.RequestAborted));
            }
            catch (Exception exception)
            {
                await _logger.LogErrorAsync<GameController>(exception);
                return Problem("Errore durante il calcolo della formazione ottimale.", statusCode: 500);
            }
        }
        [HttpPost]
        public async Task<TeamResponse> SaveTeamAsync(
            [FromQuery] string groupId,
            [FromQuery] string? leagueId,
            [FromQuery] int? year,
            [FromQuery] string? gameId,
            [FromQuery] string? editMode,
            [FromBody] Team? team,
            [FromQuery] bool? asAdmin = false)
        {
            var owner = team?.Owner ?? User.Identity?.Name ?? "unknown";
            TeamResponse? result = null;
            await _lock.ExecuteAsync(async () =>
            {
                result = await SaveTeamCoreAsync(groupId, leagueId, year, gameId, editMode, team, asAdmin);
            }, $"formation:{groupId}:{year}:{owner.ToUpperInvariant()}");
            return result ?? new TeamResponse(false, "Impossibile salvare la formazione.");
        }

        private async Task<TeamResponse> SaveTeamCoreAsync(
            string groupId,
            string? leagueId,
            int? year,
            string? gameId,
            string? editMode,
            Team? team,
            bool? asAdmin)
        {
            if (team?.Players == null)
                return new TeamResponse(false, "Formazione non valida.");
            if (User.Identity == null)
                return new TeamResponse(false, "Utente non autenticato.");
            var user = await _users.GetAsync(User.Identity.Name!);
            if (user == null)
                return new TeamResponse(false, "Utente non valido.");
            var groupForIdentity = user.Groups.FirstOrDefault(x => string.Equals(x.Id, groupId, StringComparison.OrdinalIgnoreCase));
            if (groupForIdentity == null)
                return new TeamResponse(false, "Non fai parte del gruppo.");
            if (string.IsNullOrWhiteSpace(leagueId) || year == null || string.IsNullOrWhiteSpace(gameId))
                return new TeamResponse(false, "Partita non valida.");
            var isFormationEdit = string.Equals(editMode, "formation", StringComparison.OrdinalIgnoreCase);
            var isLiveFormationChange = string.Equals(editMode, "live", StringComparison.OrdinalIgnoreCase);
            if (!isFormationEdit && !isLiveFormationChange)
                return new TeamResponse(false, "Questa partita è disponibile in sola lettura.");
            var group = await _groups.GetAsync(groupId);
            if (group == null)
                return new TeamResponse(false, "Gruppo non valido.");
            var league = group.Leagues.FirstOrDefault(x => string.Equals(x.Id, leagueId, StringComparison.OrdinalIgnoreCase));
            var annualLeague = league?.Years.FirstOrDefault(x => x.Year == year.Value);
            if (league == null || annualLeague == null)
                return new TeamResponse(false, "Lega non valida.");
            var calendar = await _calendar.GetAsync(new LeagueKey { Group = groupId, League = league.Id, Year = year.Value });
            if (calendar == null)
                return new TeamResponse(false, "Partita non valida.");
            var game = calendar.GetGameById(gameId);
            if (game.Value == null)
                return new TeamResponse(false, "Partita non valida.");
            var gameOwner = string.Equals(game.Value.HomeOwner, team.Owner, StringComparison.OrdinalIgnoreCase)
                ? game.Value.HomeOwner
                : string.Equals(game.Value.AwayOwner, team.Owner, StringComparison.OrdinalIgnoreCase)
                    ? game.Value.AwayOwner
                    : null;
            if (gameOwner == null)
                return new TeamResponse(false, "La squadra non partecipa a questa partita.");
            var basket = group.Baskets.FirstOrDefault(candidate =>
                league.BasketsId.Contains(candidate.Id, StringComparer.OrdinalIgnoreCase) &&
                candidate.Years?.Any(candidateYear => candidateYear.Year == year.Value &&
                    candidateYear.Teams?.Any(candidateTeam => string.Equals(candidateTeam.Owner, gameOwner, StringComparison.OrdinalIgnoreCase)) == true) == true);
            var annualTeam = basket?.Years.First(x => x.Year == year.Value).Teams
                .FirstOrDefault(x => string.Equals(x.Owner, gameOwner, StringComparison.OrdinalIgnoreCase));
            if (basket == null || annualTeam == null)
                return new TeamResponse(false, "Squadra non trovata nella lega.");
            if (!(annualTeam.IsOwner(User.Identity.Name!) || (asAdmin == true && groupForIdentity.Role.HasFlag(IdentityRole.SuperAdmin))))
                return new TeamResponse(false, "Non sei l'owner della squadra.");

            var realCalendar = await _realCalendar.GetAsync(year.Value);
            var liveSettings = GetLiveFormationSettings(group, basket.Id, year.Value);
            if (isFormationEdit && realCalendar?.NextDay?.SerieADay != game.SerieADay)
                return new TeamResponse(false, "Da La mia formazione puoi modificare solo la prossima giornata.");
            if (isLiveFormationChange)
            {
                if (liveSettings.Maximum <= 0)
                    return new TeamResponse(false, "Le modifiche live non sono abilitate per questa lega.");
                if (realCalendar?.LiveDay?.SerieADay != game.SerieADay)
                    return new TeamResponse(false, "Questa non è la giornata live.");
                if (!realCalendar.IsLiveFormationWindowOpen)
                    return new TeamResponse(false, "La finestra per le modifiche live è terminata.");
            }

            var teamDayKey = new TeamDayKey
            {
                Year = year.Value,
                Email = annualTeam.Owner,
                Group = groupId,
                BasketId = basket.Id,
                Day = game.SerieADay
            };
            var actualTeam = await _teamDayRepository.GetAsync(teamDayKey) ?? await _teamRepository.GetAsync(new TeamKey
            {
                Year = year.Value,
                Email = annualTeam.Owner,
                Group = groupId,
                BasketId = basket.Id,
            });
            if (actualTeam == null)
                return new TeamResponse(false, "Squadra non trovata.");
            actualTeam = actualTeam.Clone();
            actualTeam.AdditionalOwners = annualTeam.AdditionalOwners ?? [];
            var changedPlayers = actualTeam.ActivePlayers
                .Select(player => (player, submitted: team.ActivePlayers.FirstOrDefault(x => x.Key == player.Key)))
                .Where(x => x.submitted != null && x.player.Position != x.submitted!.Position)
                .ToList();
            LiveFormationChange? liveChange = null;
            if (isLiveFormationChange && changedPlayers.Count > 0)
            {
                if (actualTeam.FormationChanges >= liveSettings.Maximum)
                    return new TeamResponse(false, "Hai raggiunto il numero massimo di modifiche per questa giornata.");
                if (changedPlayers.Count != 2)
                    return new TeamResponse(false, "Durante la giornata puoi invertire solo due giocatori.");

                var oldPositions = changedPlayers.Select(x => x.player.Position).OrderBy(x => x).ToArray();
                var newPositions = changedPlayers.Select(x => x.submitted!.Position).OrderBy(x => x).ToArray();
                if (!oldPositions.SequenceEqual(newPositions))
                    return new TeamResponse(false, "La modifica deve essere uno scambio tra due giocatori.");
                if (!liveSettings.AllowModuleChange)
                {
                    static bool IsStarter(FantaSoccerRole position) => position is FantaSoccerRole.GoalKeeper or FantaSoccerRole.Defensor or FantaSoccerRole.Midfielder or FantaSoccerRole.Forward;
                    var oldModule = actualTeam.ActivePlayers.Where(x => IsStarter(x.Position)).GroupBy(x => x.Position).ToDictionary(x => x.Key, x => x.Count());
                    var newModule = actualTeam.ActivePlayers.Select(x => changedPlayers.FirstOrDefault(y => y.player.Key == x.Key).submitted?.Position ?? x.Position).Where(IsStarter).GroupBy(x => x).ToDictionary(x => x.Key, x => x.Count());
                    if (!oldModule.OrderBy(x => x.Key).SequenceEqual(newModule.OrderBy(x => x.Key)))
                        return new TeamResponse(false, "Il modulo non può essere modificato durante la giornata.");
                }
                liveChange = new LiveFormationChange
                {
                    Date = FantacalcioTime.ItalyNow,
                    Author = string.IsNullOrWhiteSpace(user.Username) ? user.Email : user.Username,
                    Players = [.. changedPlayers.Select(x => new LiveFormationPlayerChange
                    {
                        Name = x.player.Name,
                        FromPosition = x.player.Position,
                        ToPosition = x.submitted!.Position
                    })]
                };
            }
            foreach (var player in actualTeam.ActivePlayers)
            {
                var playerToSave = team.ActivePlayers.FirstOrDefault(x => x.Key == player.Key);
                if (playerToSave != null)
                    player.Position = playerToSave.Position;
            }
            var resultFromCheck = _teamChecker.Validate(actualTeam);
            if (!resultFromCheck.IsOk)
                return resultFromCheck;
            if (liveChange != null)
            {
                actualTeam.FormationChanges++;
                actualTeam.FormationChangeHistory ??= [];
                actualTeam.FormationChangeHistory.Add(liveChange);
            }
            actualTeam.LastUpdate = FantacalcioTime.ItalyNow;
            var response = await _teamDayRepository.UpdateAsync(teamDayKey, actualTeam);
            return new TeamResponse(response.IsOk, response.Message);
        }
        [HttpGet]
        public async Task<GameWrapper?> GetAsync(
            [FromQuery] string groupId,
            [FromQuery] string leagueId,
            [FromQuery] int year,
            [FromQuery] string gameId,
            [FromQuery] string? editMode = null)
        {
            if (User.Identity?.Name == null)
                return default;
            var identity = await _users.GetAsync(User.Identity.Name);
            if (identity?.Groups.Any(x => string.Equals(x.Id, groupId, StringComparison.OrdinalIgnoreCase)) != true)
                return default;
            var group = await _groups.GetAsync(groupId);
            var league = group?.Leagues.FirstOrDefault(x => string.Equals(x.Id, leagueId, StringComparison.OrdinalIgnoreCase));
            var annualLeague = league?.Years.FirstOrDefault(x => x.Year == year);
            if (group == null || league == null || annualLeague == null)
                return default;
            var calendar = await _calendar.GetAsync(new LeagueKey { Group = groupId, League = league.Id, Year = year });
            if (calendar == null)
                return default;
            var game = calendar.GetGameById(gameId);
            if (game.Value == null)
                return default;

            var ownerContexts = new List<(string Owner, Basket Basket, AnnualTeam AnnualTeam, int LiveChangesMaximum)>();
            foreach (var owner in new[] { game.Value.HomeOwner, game.Value.AwayOwner })
            {
                var basket = group.Baskets.FirstOrDefault(candidate =>
                    league.BasketsId.Contains(candidate.Id, StringComparer.OrdinalIgnoreCase) &&
                    candidate.Years?.Any(candidateYear => candidateYear.Year == year &&
                        candidateYear.Teams?.Any(candidateTeam => string.Equals(candidateTeam.Owner, owner, StringComparison.OrdinalIgnoreCase)) == true) == true);
                var annualTeam = basket?.Years.First(x => x.Year == year).Teams
                    .FirstOrDefault(x => string.Equals(x.Owner, owner, StringComparison.OrdinalIgnoreCase));
                if (basket == null || annualTeam == null)
                    return default;
                ownerContexts.Add((owner, basket, annualTeam, GetLiveFormationSettings(group, basket.Id, year).Maximum));
            }
            var leagueSettings = annualLeague.Settings ?? LeagueSetting.Default;
            var leagueType = annualLeague.Type == LeagueType.Null ? (league.Type == LeagueType.Null ? LeagueType.League : league.Type) : annualLeague.Type;
            var leagueCalculator = _leagueCalculatorFactory.Create(leagueType.ToString());
            var realCalendar = await _realCalendar.GetAsync(year);
            var nextSerieA = realCalendar?.NextDay?.SerieADay ?? 39;
            var currentLiveDay = realCalendar?.LiveDay?.SerieADay;
            var liveEditDay = ownerContexts.Any(x => x.LiveChangesMaximum > 0) ? currentLiveDay : null;
            var liveFormationDeadline = liveEditDay == game.SerieADay ? realCalendar?.LiveFormationDeadline : null;
            var isLiveFormationWindow = liveEditDay == game.SerieADay && realCalendar?.IsLiveFormationWindowOpen == true;
            var canEdit = string.Equals(editMode, "formation", StringComparison.OrdinalIgnoreCase)
                ? game.SerieADay == realCalendar?.NextDay?.SerieADay
                : string.Equals(editMode, "live", StringComparison.OrdinalIgnoreCase) && isLiveFormationWindow;
            var formationIsMutable = game.SerieADay >= nextSerieA || game.SerieADay == currentLiveDay;
            var hasNoResult = game.Value.HasNoResult;
            int? liveFormationChangesRemaining = null;
            await _teamCalculator.BuildRequestAsync(year, game.SerieADay, leagueType, leagueSettings, true, !formationIsMutable, true);
            var teams = new List<LiveTeam>();
            foreach (var context in ownerContexts)
            {
                var (owner, basket, annualTeam, liveChangesMaximum) = context;
                var isInHome = string.Equals(game.Value.HomeOwner, owner, StringComparison.OrdinalIgnoreCase);
                var teamDayKey = new TeamDayKey
                {
                    Year = year,
                    Email = annualTeam.Owner,
                    Group = groupId,
                    BasketId = basket.Id,
                    Day = game.SerieADay
                };
                var team = await _teamDayRepository.GetAsync(teamDayKey);
                if (team == null && formationIsMutable)
                {
                    team = await _teamRepository.GetAsync(new TeamKey
                    {
                        Year = year,
                        Email = annualTeam.Owner,
                        Group = groupId,
                        BasketId = basket.Id
                    });
                }
                if (liveEditDay == game.SerieADay && annualTeam.IsOwner(User.Identity.Name))
                    liveFormationChangesRemaining = Math.Max(0, liveChangesMaximum - (team?.FormationChanges ?? 0));
                var currentPlayers = _teamCalculator.Enrich(team?.ActivePlayers ?? []) ?? [];
                if (!formationIsMutable)
                    currentPlayers = leagueCalculator?.GetRightFormation(currentPlayers) ?? [];
                teams.Add(new LiveTeam
                {
                    Name = team?.Name ?? (isInHome ? game.Value.Home : game.Value.Away),
                    Owner = team?.Owner ?? annualTeam.Owner,
                    AdditionalOwners = team?.AdditionalOwners ?? annualTeam.AdditionalOwners ?? [],
                    Players = [.. currentPlayers.Select(x => new LivePlayer
                    {
                        Current = x.Current,
                        Chance = x.Chance,
                        CurrentPosition = x.CurrentPosition,
                        FinalValue = x.FinalValue,
                        RealGame = x.RealGame,
                        Vote = x.Vote,
                    })],
                    LastUpdate = team?.LastUpdate ?? DateTime.UtcNow,
                    FormationChangesMaximum = liveEditDay == game.SerieADay ? liveChangesMaximum : null,
                    FormationChangesUsed = liveEditDay == game.SerieADay ? team?.FormationChanges ?? 0 : null,
                    FormationChangesRemaining = liveEditDay == game.SerieADay
                        ? Math.Max(0, liveChangesMaximum - (team?.FormationChanges ?? 0))
                        : null,
                    FormationChangeHistory = team?.FormationChangeHistory ?? [],
                });
                if (!formationIsMutable && hasNoResult)
                {
                    game.Value.Result ??= new();
                    if (team?.ActivePlayers != null)
                    {
                        if (isInHome)
                            game.Value.Result.Home = _teamCalculator.CalculatePoint(team.ActivePlayers);
                        else
                            game.Value.Result.Away = _teamCalculator.CalculatePoint(team.ActivePlayers);
                    }
                }
            }
            if (!formationIsMutable && game.Value.Result != null && _teamCalculator.LeagueSetting != null)
                game.Value.Result.CalculateGoals(_teamCalculator.LeagueSetting);
            return new GameWrapper
            {
                Game = game.Value,
                SerieADay = game.SerieADay,
                Teams = teams,
                CanEdit = canEdit,
                IsLiveFormationWindow = isLiveFormationWindow,
                LiveFormationDeadline = liveFormationDeadline,
                LiveFormationChangesRemaining = liveFormationChangesRemaining,
            };
        }

        private static (int Maximum, bool AllowModuleChange) GetLiveFormationSettings(Group group, string basketId, int year)
        {
            // La formazione è condivisa dal basket: tra più leghe prevale la regola più restrittiva.
            var settings = group.Leagues
                .Where(league => league.BasketsId.Contains(basketId, StringComparer.OrdinalIgnoreCase))
                .Select(league => league.Years.FirstOrDefault(annual => annual.Year == year)?.Settings)
                .Where(setting => setting != null)
                .Cast<LeagueSetting>()
                .ToList();
            return settings.Count == 0
                ? (0, false)
                : (settings.Min(x => x.LiveFormationChanges), settings.All(x => x.AllowLiveModuleChange));
        }
    }
}

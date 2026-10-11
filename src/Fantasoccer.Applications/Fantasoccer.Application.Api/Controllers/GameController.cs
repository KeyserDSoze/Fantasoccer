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
        private readonly ILogger<GameController> _telemetryLogger;
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
            IOptimalFormationService optimalFormationService,
            ILogger<GameController> telemetryLogger)
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
            _telemetryLogger = telemetryLogger;
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
                var league = group?.Leagues?.FirstOrDefault(x => string.Equals(x.Id, leagueId, StringComparison.OrdinalIgnoreCase));
                var annualLeague = league?.Years?.FirstOrDefault(x => x.Year == year);
                var basket = group?.Baskets?.FirstOrDefault(x =>
                    league?.BasketsId?.Contains(x.Id, StringComparer.OrdinalIgnoreCase) == true &&
                    x.Years?.FirstOrDefault(y => y.Year == year)?.Teams?.Any(t => string.Equals(t.Owner, owner, StringComparison.OrdinalIgnoreCase)) == true);
                var annualTeam = basket?.Years?.FirstOrDefault(x => x.Year == year)?.Teams
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
                _telemetryLogger.LogError(
                    exception,
                    "OptimalFormation failed for group {GroupId}, league {LeagueId}, year {Year}, day {Day}, owner {Owner}",
                    groupId,
                    leagueId,
                    year,
                    day,
                    owner);
                await _logger.LogErrorAsync<GameController>(exception);
                return Problem("Errore durante il calcolo della formazione ottimale.", statusCode: 500);
            }
        }
        [HttpPost]
        public async Task<TeamResponse> SwitchAsync(
            [FromQuery] string groupId, [FromQuery] string leagueId, [FromQuery] int year,
            [FromQuery] string gameId, [FromQuery] bool? asAdmin = false,
            [FromBody] SwitchPlayers? players = null)
        {
            TeamResponse? result = null;
            await _lock.ExecuteAsync(async () =>
            {
                result = await SwitchCoreAsync(groupId, leagueId, year, gameId, asAdmin, players);
            }, $"formation-switch:{groupId}:{year}:{gameId}");
            return result ?? new TeamResponse(false, "Impossibile applicare il cambio.");
        }

        private async Task<TeamResponse> SwitchCoreAsync(
            string groupId, string leagueId, int year, string gameId, bool? asAdmin, SwitchPlayers? players)
        {
            if (players?.PlayerKeys == null || players.PlayerKeys.Count != 2 || players.PlayerKeys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 2)
                return new TeamResponse(false, "Indicare esattamente due giocatori distinti.");
            if (User.Identity?.Name == null) return new TeamResponse(false, "Utente non autenticato.");
            var user = await _users.GetAsync(User.Identity.Name);
            var group = await _groups.GetAsync(groupId);
            var league = group?.Leagues.FirstOrDefault(x => string.Equals(x.Id, leagueId, StringComparison.OrdinalIgnoreCase));
            var annualLeague = league?.Years.FirstOrDefault(x => x.Year == year);
            var identityGroup = user?.Groups.FirstOrDefault(x => string.Equals(x.Id, groupId, StringComparison.OrdinalIgnoreCase));
            if (user == null || group == null || league == null || annualLeague == null || identityGroup == null)
                return new TeamResponse(false, "Contesto non valido.");
            var calendar = await _calendar.GetAsync(new LeagueKey { Group = groupId, League = league.Id, Year = year });
            var gameResult = calendar?.GetGameById(gameId);
            var game = gameResult?.Value;
            if (game == null) return new TeamResponse(false, "Partita non valida.");
            var realCalendar = await _realCalendar.GetAsync(year);
            var liveDay = realCalendar?.LiveDay?.SerieADay ?? realCalendar?.NextDay?.SerieADay;
            if (liveDay != gameResult!.Value.SerieADay || realCalendar?.IsLiveFormationWindowOpen != true)
                return new TeamResponse(false, "La finestra per le modifiche live è terminata.");
            var candidates = new List<(TeamDayKey Key, Team Team, AnnualTeam AnnualTeam)>();
            foreach (var owner in new[] { game.HomeOwner, game.AwayOwner })
            {
                var basket = group.Baskets.FirstOrDefault(b => league.BasketsId.Contains(b.Id, StringComparer.OrdinalIgnoreCase) && b.Years.Any(y => y.Year == year && y.Teams.Any(t => string.Equals(t.Owner, owner, StringComparison.OrdinalIgnoreCase))));
                var annualTeam = basket?.Years.FirstOrDefault(y => y.Year == year)?.Teams.FirstOrDefault(t => string.Equals(t.Owner, owner, StringComparison.OrdinalIgnoreCase));
                if (basket == null || annualTeam == null) continue;
                var key = new TeamDayKey { Group = groupId, BasketId = basket.Id, Email = owner, Year = year, Day = gameResult.Value.SerieADay };
                var team = await _teamDayRepository.GetAsync(key);
                if (team != null) candidates.Add((key, team, annualTeam));
            }
            var candidate = candidates.FirstOrDefault(x => x.Team.ActivePlayers.Count(p => players.PlayerKeys.Contains(p.Key, StringComparer.OrdinalIgnoreCase)) == 2);
            if (candidate.Team == null) return new TeamResponse(false, "Squadra giornaliera non trovata.");
            var isAdmin = asAdmin == true && identityGroup.Role.HasFlag(IdentityRole.SuperAdmin);
            if (!candidate.AnnualTeam.IsOwner(User.Identity.Name) && !isAdmin) return new TeamResponse(false, "Non sei l'owner della squadra.");
            var settings = GetFormationLeague(group, league, candidate.Key.BasketId, year).Settings ?? LeagueSetting.Default;
            if (settings.LiveFormationChanges <= 0) return new TeamResponse(false, "Le modifiche live non sono abilitate per questa lega.");
            if (candidate.Team.FormationChanges >= settings.LiveFormationChanges) return new TeamResponse(false, "Hai raggiunto il numero massimo di modifiche per questa giornata.");
            var selected = candidate.Team.ActivePlayers.Where(p => players.PlayerKeys.Contains(p.Key, StringComparer.OrdinalIgnoreCase)).ToList();
            if (selected.Count != 2 || selected[0].Role != selected[1].Role || selected[0].Role is not (Role.GoalKeeper or Role.Defensor or Role.Midfielder or Role.Forward))
                return new TeamResponse(false, "I giocatori devono avere lo stesso ruolo principale.");
            var switchedTeam = candidate.Team.Clone();
            var first = switchedTeam.ActivePlayers.First(p => p.Key == selected[0].Key);
            var second = switchedTeam.ActivePlayers.First(p => p.Key == selected[1].Key);
            var firstPosition = first.Position;
            var secondPosition = second.Position;
            (first.Position, second.Position) = (second.Position, first.Position);
            var validation = _teamChecker.Validate(switchedTeam);
            if (!validation.IsOk) return validation;
            switchedTeam.FormationChanges++;
            switchedTeam.FormationChangeHistory ??= [];
            switchedTeam.FormationChangeHistory.Add(new LiveFormationChange { Date = FantacalcioTime.ItalyNow, Author = string.IsNullOrWhiteSpace(user.Username) ? user.Email : user.Username, Players = [new() { Name = first.Name, FromPosition = firstPosition, ToPosition = secondPosition }, new() { Name = second.Name, FromPosition = secondPosition, ToPosition = firstPosition }] });
            switchedTeam.LastUpdate = FantacalcioTime.ItalyNow;
            var saved = await _teamDayRepository.UpdateAsync(candidate.Key, switchedTeam);
            return new TeamResponse(saved.IsOk, saved.Message);
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
            var isSuperAdmin = asAdmin == true && groupForIdentity.Role.HasFlag(IdentityRole.SuperAdmin);
            if (!(annualTeam.IsOwner(User.Identity.Name!) || isSuperAdmin))
                return new TeamResponse(false, "Non sei l'owner della squadra.");

            var realCalendar = await _realCalendar.GetAsync(year.Value);
            var currentLiveDay = realCalendar?.LiveDay?.SerieADay;
            var liveEditDay = currentLiveDay ?? realCalendar?.NextDay?.SerieADay;
            var isUpcomingLiveDay = currentLiveDay == null && liveEditDay == game.SerieADay;
            var isLiveFormationWindowOpen =
                (liveEditDay == game.SerieADay && realCalendar?.IsLiveFormationWindowOpen == true) ||
                isUpcomingLiveDay;
            var liveSettings = GetLiveFormationSettings(GetFormationLeague(group, league, basket.Id, year.Value));
            if (isFormationEdit && !isSuperAdmin && realCalendar?.NextDay?.SerieADay != game.SerieADay)
                return new TeamResponse(false, "Da La mia formazione puoi modificare solo la prossima giornata.");
            if (isLiveFormationChange)
            {
                if (liveSettings.Maximum <= 0)
                    return new TeamResponse(false, "Le modifiche live non sono abilitate per questa lega.");
                if (liveEditDay != game.SerieADay)
                    return new TeamResponse(false, "Questa non è la giornata live.");
                if (!isLiveFormationWindowOpen)
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
            var dailyTeam = await _teamDayRepository.GetAsync(teamDayKey);
            var actualTeam = dailyTeam ?? await _teamRepository.GetAsync(new TeamKey
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
            if (dailyTeam != null)
            {
                var response = await _teamDayRepository.UpdateAsync(teamDayKey, actualTeam);
                return new TeamResponse(response.IsOk, response.Message);
            }
            if (isSuperAdmin && realCalendar?.NextDay?.SerieADay != game.SerieADay)
            {
                var response = await _teamDayRepository.InsertAsync(teamDayKey, actualTeam);
                return new TeamResponse(response.IsOk, response.Message);
            }

            var generalResponse = await _teamRepository.UpdateAsync(new TeamKey
                {
                    Year = year.Value,
                    Email = annualTeam.Owner,
                    Group = groupId,
                    BasketId = basket.Id,
                }, actualTeam);
            return new TeamResponse(generalResponse.IsOk, generalResponse.Message);
        }
        [HttpGet]
        public async Task<GameWrapper?> GetAsync(
            [FromQuery] string groupId,
            [FromQuery] string leagueId,
            [FromQuery] int year,
            [FromQuery] string gameId,
            [FromQuery] string? editMode = null,
            [FromQuery] bool? asAdmin = false)
        {
            if (User.Identity?.Name == null)
                return default;
            var identity = await _users.GetAsync(User.Identity.Name);
            if (identity?.Groups.Any(x => string.Equals(x.Id, groupId, StringComparison.OrdinalIgnoreCase)) != true)
                return default;
            var groupForIdentity = identity.Groups.First(x => string.Equals(x.Id, groupId, StringComparison.OrdinalIgnoreCase));
            var isSuperAdmin = asAdmin == true && groupForIdentity.Role.HasFlag(IdentityRole.SuperAdmin);
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
                var formationLeague = GetFormationLeague(group, league, basket.Id, year);
                ownerContexts.Add((owner, basket, annualTeam, GetLiveFormationSettings(formationLeague).Maximum));
            }
            var leagueSettings = annualLeague.Settings ?? LeagueSetting.Default;
            var leagueType = annualLeague.Type == LeagueType.Null ? (league.Type == LeagueType.Null ? LeagueType.League : league.Type) : annualLeague.Type;
            var leagueCalculator = _leagueCalculatorFactory.Create(leagueType.ToString());
            var realCalendar = await _realCalendar.GetAsync(year);
            var nextSerieA = realCalendar?.NextDay?.SerieADay ?? 39;
            var currentLiveDay = realCalendar?.LiveDay?.SerieADay;
            var liveEditDay = ownerContexts.Any(x => x.LiveChangesMaximum > 0)
                ? currentLiveDay ?? realCalendar?.NextDay?.SerieADay
                : null;
            var liveFormationDeadline = liveEditDay == game.SerieADay ? realCalendar?.LiveFormationDeadline : null;
            var isUpcomingLiveDay = currentLiveDay == null && liveEditDay == game.SerieADay;
            var isLiveFormationWindow = liveEditDay == game.SerieADay &&
                (realCalendar?.IsLiveFormationWindowOpen == true || isUpcomingLiveDay);
            var formationIsMutable = game.SerieADay >= nextSerieA || game.SerieADay == currentLiveDay;
            var canEdit = string.Equals(editMode, "formation", StringComparison.OrdinalIgnoreCase)
                ? (isSuperAdmin || game.SerieADay == realCalendar?.NextDay?.SerieADay)
                : string.Equals(editMode, "live", StringComparison.OrdinalIgnoreCase) && isLiveFormationWindow;
            var hasNoResult = game.Value.HasNoResult;
            int? liveFormationChangesRemaining = null;
            var shouldLoadVotes = currentLiveDay == game.SerieADay ||
                game.SerieADay <= (realCalendar?.LastDay?.SerieADay ?? 0) ||
                !hasNoResult;
            await _teamCalculator.BuildRequestAsync(year, game.SerieADay, leagueType, leagueSettings, true, shouldLoadVotes, true);
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
                if (team == null && (formationIsMutable || isSuperAdmin))
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
                // Keep automatic substitutions in the displayed formation; Game/Switch never uses this projection to save.
                currentPlayers = leagueCalculator?.GetRightFormation(currentPlayers) ?? [];
                teams.Add(new LiveTeam
                {
                    Name = team?.Name ?? (isInHome ? game.Value.Home : game.Value.Away),
                    Owner = team?.Owner ?? annualTeam.Owner,
                    AdditionalOwners = annualTeam.AdditionalOwners ?? [],
                    Players = [.. currentPlayers.Select(x =>
                    {
                        var isGameUpcoming = x.RealGame?.Date is DateTime gameDate &&
                            gameDate > FantacalcioTime.ItalyNow &&
                            x.RealGame.HomeGoal == null &&
                            x.RealGame.AwayGoal == null;
                        return new LivePlayer
                        {
                            Current = x.Current,
                            Chance = x.Vote == null && isGameUpcoming ? x.Chance : null,
                            CurrentPosition = x.CurrentPosition,
                            FinalValue = x.FinalValue,
                            RealGame = x.RealGame,
                            Vote = x.Vote,
                        };
                    })],
                    LastUpdate = team?.LastUpdate ?? DateTime.UtcNow,
                    FormationChangesMaximum = liveEditDay == game.SerieADay ? liveChangesMaximum : null,
                    FormationChangesUsed = liveEditDay == game.SerieADay ? team?.FormationChanges ?? 0 : null,
                    FormationChangesRemaining = liveEditDay == game.SerieADay
                        ? Math.Max(0, liveChangesMaximum - (team?.FormationChanges ?? 0))
                        : null,
                    FormationChangeHistory = team?.FormationChangeHistory ?? [],
                });
                if ((currentLiveDay == game.SerieADay || !formationIsMutable) && hasNoResult)
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
            if ((currentLiveDay == game.SerieADay || !formationIsMutable) &&
                game.Value.Result?.Home != null &&
                game.Value.Result.Away != null &&
                _teamCalculator.LeagueSetting != null)
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

        // A formation is shared by basket, even when it is viewed through a secondary competition.
        // Use that basket's main league for editing rules, but keep the viewed league for scoring.
        private static AnnualLeague GetFormationLeague(Group group, League viewedLeague, string basketId, int year)
        {
            return group.Leagues
                .Where(league => league.IsMain && league.BasketsId.Contains(basketId, StringComparer.OrdinalIgnoreCase))
                .SelectMany(league => league.Years)
                .FirstOrDefault(annualLeague => annualLeague.Year == year)
                ?? viewedLeague.Years.First(annualLeague => annualLeague.Year == year);
        }

        private static (int Maximum, bool AllowModuleChange) GetLiveFormationSettings(AnnualLeague annualLeague)
        {
            var settings = annualLeague.Settings ?? LeagueSetting.Default;
            return (settings.LiveFormationChanges, settings.AllowLiveModuleChange);
        }
    }

    public sealed class SwitchPlayers
    {
        public List<string> PlayerKeys { get; set; } = [];
    }
}

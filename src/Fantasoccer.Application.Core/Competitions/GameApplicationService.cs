using System.Security.Claims;
using Fantasoccer.Application.Core.Football;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Application.Core.Teams;
using Fantasoccer.Business;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Application.Core.Competitions;

public sealed class GameApplicationService(FantasoccerDbContext database, CompetitionApplicationService competitions,
    TeamRosterApplicationService rosters, FootballApplicationService football, ITeamCalculator calculator,
    IFactory<ILeagueCalculator> leagues, IOptimalFormationService optimal, TimeProvider clock)
{
    public async Task<GameDto> GetAsync(ClaimsPrincipal caller, Guid group, Guid league, int year, Guid matchId,
        string editMode = "view", bool asAdmin = false, CancellationToken cancellationToken = default)
    {
        var season = await competitions.AuthorizeAsync(caller, group, league, year, cancellationToken);
        if (editMode is not ("view" or "formation" or "live")) throw new ApplicationFault(400, "invalid_game_mode", "Modalità partita non valida.");
        var match = await MatchAsync(group, league, year, matchId, cancellationToken);
        var rules = await database.LeagueRules.AsNoTracking().SingleAsync(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year, cancellationToken);
        var settings = LeagueRulesMapping.ToDomain(rules);
        var type = (LeagueType)(season.CompetitionType != 0 ? season.CompetitionType : season.League.CompetitionType != 0 ? season.League.CompetitionType : 1);
        var day = match.Matchday.SerieADay;
        await calculator.BuildRequestAsync(year, day, type, settings, true, true, true);
        var schedule = await database.FootballMatches.Where(x => x.SeasonIndex == year && x.SerieADay == day && (x.WasPostponed ? x.OriginalKickoffUtc : x.KickoffUtc) != null)
            .Select(x => (x.WasPostponed ? x.OriginalKickoffUtc : x.KickoffUtc)!.Value).ToArrayAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var before = schedule.Length > 0 && now < schedule.Min();
        var deadline = schedule.Length > 0 ? (DateTimeOffset?)schedule.Max().AddHours(4) : null;
        var liveWindow = schedule.Length > 0 && now >= schedule.Min() && now <= deadline;
        var email = caller.FindFirstValue(ClaimTypes.Email)!.Trim().ToLowerInvariant();
        var admin = asAdmin && await database.GroupMemberships.AnyAsync(x => x.GroupId == group && x.User.Email == email && (x.Roles & GroupRole.SuperAdmin) != 0, cancellationToken);
        var calendar = await football.CalendarAsync(caller, year, cancellationToken);
        var footballDay = calendar?.Days.SingleOrDefault(x => x.SerieADay == day);
        var teamDtos = new List<LiveTeamDto>();
        var points = new List<Point>();
        var preview = false;
        var result = CompetitionApplicationService.Game(match);
        foreach (var id in new[] { match.HomeTeamId, match.AwayTeamId })
        {
            if (id is null) continue;
            var metadata = await database.TeamSeasons.AsNoTracking().Include(x => x.Owners).ThenInclude(x => x.Membership).ThenInclude(x => x.User)
                .SingleAsync(x => x.Id == id && x.GroupId == group && x.SeasonIndex == year, cancellationToken);
            var owner = metadata.Owners.SingleOrDefault(x => x.IsPrimary)?.Membership.User.OwnerReference ?? (id == match.HomeTeamId ? match.HomeOwner : match.AwayOwner) ?? "";
            var daily = await rosters.GetAsync(caller, group, metadata.BasketId, year, owner, RosterKind.Matchday, day, cancellationToken: cancellationToken, teamId: id);
            var source = daily;
            if (source is null && (before || liveWindow))
                source = await rosters.GetAsync(caller, group, metadata.BasketId, year, owner, cancellationToken: cancellationToken, teamId: id);
            var historicalFallback = false;
            if (source is null)
            {
                source = await PlayedRosterAsync(match, metadata, day, id == match.HomeTeamId, cancellationToken);
                historicalFallback = source != null;
            }
            if (source is null) throw new ApplicationFault(409, "historical_roster_unavailable", "Snapshot storico della formazione non disponibile; non viene sostituito con la rosa attuale.");
            var formationRules = await rosters.RulesAsync(metadata, year, league, false, cancellationToken);
            var remaining = Math.Max(0, formationRules.LiveFormationChanges - (daily?.FormationChanges ?? 0));
            var owned = metadata.Owners.Any(x => x.Membership.User.Email == email);
            var editable = !historicalFallback && !metadata.IsArchived && (owned || admin) && editMode != "view" &&
                (admin || (editMode == "live" ? liveWindow && daily != null && remaining > 0 : before));
            var active = source.Players.Where(x => x.Status == 0).Select(x => (Wire: x, Domain: DomainPlayer(x))).ToArray();
            var wire = active.ToDictionary(x => x.Domain, x => x.Wire);
            var projection = leagues.Create((settings.Formation == FormationType.Best && editMode == "view" ? LeagueType.SuperLeague : type).ToString())
                ?? throw new ApplicationFault(409, "league_calculator_unavailable", "Calcolatore non disponibile per questa competizione.");
            var enriched = calculator.Enrich(active.Select(x => x.Domain)).ToArray();
            if (editMode == "view") enriched = projection.GetRightFormation(enriched).ToArray();
            var point = calculator.CalculatePoint(active.Select(x => DomainPlayer(x.Wire)));
            if (id == match.HomeTeamId) point = new(point.Value + settings.PointInHome, point.DefensiveBonus, point.GoodPeople, point.OwnGoal);
            points.Add(point);
            var players = enriched.Select(x => new LivePlayerDto(wire[x.Current], x.Vote is null ? null : Vote(x.Vote),
                x.Chance is null ? null : new(x.Chance.Fantagazzetta, x.Chance.Gazzetta, x.Chance.Mediaset, x.Chance.Sky, (int)x.Chance.Status, x.Chance.Description),
                footballDay?.Games.FirstOrDefault(game => string.Equals(game.Home.Name, x.Current.RealTeam.Name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(game.Away.Name, x.Current.RealTeam.Name, StringComparison.OrdinalIgnoreCase)),
                x.FinalValue is null ? null : new(x.FinalValue.Value, x.FinalValue.Special), (int)x.CurrentPosition)).ToArray();
            teamDtos.Add(new(metadata.Id, metadata.BasketId, daily?.Version ?? 0, daily is null && !historicalFallback ? source.Version : null, source.Name, source.Owner, source.AdditionalOwners,
                players, source.LastUpdate, formationRules.LiveFormationChanges, daily?.FormationChanges ?? 0, remaining, source.FormationChangeHistory, source.LogoUrl, editable));
        }
        // Saved results are authoritative. A preview is explicitly marked and is
        // never written back by a GET or used to replace imported history.
        if (result.Result is null && teamDtos.Count == 2 && (calculator.Live != null || calculator.Official != null))
        {
            var domainResult = new GameResult { Home = points[0], Away = points[1] };
            var goals = domainResult.CalculateGoals(settings);
            result = result with { Result = new(new(points[0].Value, points[0].DefensiveBonus, points[0].GoodPeople, points[0].OwnGoal),
                new(points[1].Value, points[1].DefensiveBonus, points[1].GoodPeople, points[1].OwnGoal), false, goals.Home, goals.Away) };
            preview = true;
        }
        return new(teamDtos.ToArray(), result, day, teamDtos.Any(x => x.CanEdit), liveWindow, deadline,
            teamDtos.Where(x => x.CanEdit).Select(x => (int?)x.FormationChangesRemaining).FirstOrDefault(), preview)
            { AwaitingPostponedMatches = match.Matchday.AwaitingPostponedMatches };
    }

    public async Task<FormationSavedDto> SaveAsync(ClaimsPrincipal caller, Guid group, Guid league, int year, Guid matchId,
        SaveGameFormationRequest request, CancellationToken cancellationToken = default)
    {
        await competitions.AuthorizeAsync(caller, group, league, year, cancellationToken);
        var match = await MatchAsync(group, league, year, matchId, cancellationToken);
        if (request.TeamId != match.HomeTeamId && request.TeamId != match.AwayTeamId)
            throw new ApplicationFault(400, "team_not_in_match", "La squadra non appartiene alla partita.");
        var metadata = await database.TeamSeasons.AsNoTracking().Include(x => x.Owners).ThenInclude(x => x.Membership).ThenInclude(x => x.User)
            .SingleAsync(x => x.GroupId == group && x.Id == request.TeamId && x.SeasonIndex == year, cancellationToken);
        var owner = metadata.Owners.Single(x => x.IsPrimary).Membership.User.OwnerReference;
        return await rosters.SaveFormationAsync(caller, new(group, metadata.BasketId, year, owner, match.Matchday.SerieADay, league,
            (int)RosterKind.Matchday, request.Positions, request.ExpectedVersion, request.Live, request.AsAdmin, metadata.Id, request.SourceVersion, matchId), cancellationToken);
    }

    public async Task<OptimalGameFormationDto> OptimalAsync(ClaimsPrincipal caller, Guid group, Guid league, int year, int day, string owner, bool asAdmin,
        CancellationToken cancellationToken = default)
    {
        var season = await competitions.AuthorizeAsync(caller, group, league, year, cancellationToken);
        if (day is < 1 or > 38) throw new ApplicationFault(400, "invalid_formation_day", "Giornata non valida.");
        var email = caller.FindFirstValue(ClaimTypes.Email)!.Trim().ToLowerInvariant();
        var canonical = owner.Trim().ToLowerInvariant();
        var metadata = await database.TeamSeasons.AsNoTracking().Include(x => x.Owners).ThenInclude(x => x.Membership).ThenInclude(x => x.User)
            .Where(x => x.GroupId == group && x.SeasonIndex == year && !x.IsArchived && x.Owners.Any(o => o.IsPrimary && o.Membership.User.Email == canonical) &&
                database.LeagueBasketSeasons.Any(p => p.GroupId == group && p.LeagueId == league && p.SeasonIndex == year && p.BasketId == x.BasketId)).ToArrayAsync(cancellationToken);
        if (metadata.Length != 1) throw new ApplicationFault(409, "ambiguous_formation_team", "Specificare una squadra univoca nella competizione.");
        var team = metadata[0];
        var admin = asAdmin && await database.GroupMemberships.AnyAsync(x => x.GroupId == group && x.User.Email == email && (x.Roles & GroupRole.SuperAdmin) != 0, cancellationToken);
        if (!admin && !team.Owners.Any(x => x.Membership.User.Email == email)) throw new ApplicationFault(403, "formation_access_denied", "Non sei proprietario della squadra.");
        var roster = await rosters.GetAsync(caller, group, team.BasketId, year, owner, RosterKind.Matchday, day, cancellationToken: cancellationToken, teamId: team.Id)
            ?? await rosters.GetAsync(caller, group, team.BasketId, year, owner, cancellationToken: cancellationToken, teamId: team.Id)
            ?? throw new ApplicationFault(404, "roster_not_found", "Rosa non trovata.");
        var rules = await rosters.RulesAsync(team, year, league, false, cancellationToken);
        var domain = new Team { Name = roster.Name, Owner = roster.Owner, AdditionalOwners = roster.AdditionalOwners.ToList(), Players = roster.Players.Select(DomainPlayer).ToList() };
        var result = await optimal.CalculateAsync(domain, year, day, (LeagueType)(season.CompetitionType == 0 ? 1 : season.CompetitionType), LeagueRulesMapping.ToDomain(rules), cancellationToken);
        var identities = roster.Players.Where(x => x.Status == 0).ToDictionary(x => KeyUtils.GetPlayerKey(x.Name), x => x.PlayerId);
        return new(result.Players.Select(x => new OptimalGamePlayerDto(identities[x.PlayerKey], x.PlayerKey, x.PlayerName, (int)x.Role, (int)x.Position, x.Projection, x.Confidence)).ToArray(),
            result.Module, result.Score, result.Confidence, result.Warnings.ToArray());
    }
    private async Task<FantasyMatch> MatchAsync(Guid group, Guid league, int year, Guid id, CancellationToken cancellationToken) =>
        await database.FantasyMatches.AsNoTracking().Include(x => x.Matchday).SingleOrDefaultAsync(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year && x.Id == id, cancellationToken)
            ?? throw new ApplicationFault(404, "match_not_found", "Partita non trovata nella competizione richiesta.");

    private async Task<TeamRosterDto?> PlayedRosterAsync(FantasyMatch match, FantasyTeamSeason metadata, int day, bool home, CancellationToken cancellationToken)
    {
        var saved = await database.PlayedMatchSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.GroupId == match.GroupId && x.LeagueId == match.LeagueId &&
            x.SeasonIndex == match.SeasonIndex && x.MatchId == match.Id && x.Day == day, cancellationToken);
        if (saved is null) return null;
        var rows = saved.Players.Where(x => x.IsHome == home).OrderBy(x => x.Ordinal).ToArray();
        var playerIds = rows.Select(x => x.PlayerId).ToArray();
        var clubIds = rows.Select(x => x.ClubId).ToArray();
        var players = await database.Players.Where(x => playerIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var clubs = await database.Clubs.Where(x => clubIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        return new(metadata.Id, match.GroupId, metadata.BasketId, match.SeasonIndex, (int)RosterKind.Matchday, day, null,
            home ? saved.HomeName : saved.AwayName, (home ? match.HomeOwner : match.AwayOwner) ?? "", [],
            rows.Select(x => new RosterPlayerDto(x.PlayerId, x.Ordinal, players[x.PlayerId].Name, new(clubs[x.ClubId].Name, clubs[x.ClubId].Id),
                x.PlayerRole, x.IsActive, x.IsVisible, x.Price, x.Revenue, x.Status, x.Position)
                { SeasonIndex = match.SeasonIndex }).ToArray(), 0, 0, null, 0, [], 0);
    }
    private static Player DomainPlayer(RosterPlayerDto row) => new() { Name = row.Name, RealTeam = new() { Name = row.Team.Name, Abbreviation = row.Team.Abbreviation },
        Role = (Role)row.Role, IsActive = row.IsActive, Visible = row.Visible, Price = row.Price, Revenue = row.Revenue, Status = (PlayerInTeamStatus)row.Status, Position = (FantaSoccerRole)row.Position };
    private static VoteDto Vote(Vote row) => new((int)row.Role, row.Value, row.IsFinal, row.Goal, row.Penalty, row.Assist, row.StoppedPenalty, row.SufferedGoal,
        row.WrongedPenalty, row.OwnGoal, (int)row.Status, row.ManOfTheMatch, row.HasVote, row.IsOut, row.IsIn, row.Injured) { IsPolitical = row.IsPolitical };
}

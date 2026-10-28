using System.Security.Claims;
using System.Security.Cryptography;
using Fantasoccer.Application.Core.Competitions;
using Fantasoccer.Application.Core.Football;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Application.Core.Teams;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Application.Core.Auctions;

public sealed partial class AuctionApplicationService(FantasoccerDbContext database, CompetitionApplicationService competitions,
    TeamRosterApplicationService rosters, TeamRosterAvailabilityService availability, FootballApplicationService football, IAuctionUpdatePublisher publisher, TimeProvider clock)
{
    public async Task<Guid> CreateAsync(ClaimsPrincipal caller, CreateAuctionRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Type is < 0 or > 2 || request.Kind is < 0 or > 1) throw Invalid("Tipo di asta non valido.");
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var season = await competitions.AuthorizeAsync(caller, request.GroupId, request.LeagueId, request.Year, cancellationToken);
        // The auction is an economic act of autonomous competitions only: derived
        // teams resolve the origin's roster and never buy players themselves.
        if (season.League.Mode == LeagueMode.Derived) throw new ApplicationFault(409, "derived_league_economy", "Le leghe derivate non hanno asta: si usa quella della squadra originale.");
        var actor = await ActorAsync(caller, request.GroupId, cancellationToken);
        if (!IsAuctioneer(actor.Roles) || !season.League.IsMain || season.IsArchived || season.League.IsArchived || request.Year != FantacalcioTime.SeasonYearAt(clock.GetUtcNow().UtcDateTime)) throw Denied();
        var teams = await TeamsAsync(request.GroupId, request.LeagueId, request.Year, cancellationToken);
        if (teams.Length == 0) throw Invalid("Nessuna squadra partecipa alla lega.");
        var basketIds = teams.Select(x => x.BasketId).Distinct().ToArray();
        var existing = await database.RosterSnapshots.Where(x => x.GroupId == request.GroupId && x.SeasonIndex == request.Year && basketIds.Contains(x.BasketId) && x.Kind == RosterKind.Season)
            .SelectMany(x => x.Players).Select(x => x.PlayerId).Distinct().ToArrayAsync(cancellationToken);
        var players = await database.PlayerSeasons.AsNoTracking().Include(x => x.Player).Where(x => x.SeasonIndex == request.Year && x.Position >= 0 && x.ClubId != null && !existing.Contains(x.PlayerId))
            .OrderBy(x => x.Position).ThenBy(x => x.Player.Name).ToArrayAsync(cancellationToken);
        if (players.Length == 0) throw Invalid("Non ci sono giocatori disponibili per questa asta.");
        var auction = new AuctionSession { GroupId = request.GroupId, LeagueId = request.LeagueId, SeasonIndex = request.Year, Id = Guid.NewGuid(), CreatorUserId = actor.Id,
            CreatorEmail = actor.Email, CreatedAtUtc = clock.GetUtcNow(), Type = request.Type, Kind = request.Kind };
        foreach (var group in players.GroupBy(x => (int)x.Position))
        {
            IEnumerable<PlayerSeason> ordered = group;
            if (request.Type == 1)
            {
                var firstLetter = RandomNumberGenerator.GetInt32(26);
                ordered = group.OrderBy(x => ((char.ToUpperInvariant(x.Player.Name[0]) - 'A' - firstLetter + 26) % 26)).ThenBy(x => x.Player.Name);
            }
            if (request.Type == 2) ordered = group.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue));
            foreach (var (player, ordinal) in ordered.Select((player, ordinal) => (player, ordinal)))
                auction.PlayerOrder.Add(new() { Role = group.Key, Ordinal = ordinal, PlayerId = player.PlayerId });
        }
        for (var role = 0; role < 4; role++) auction.LastShown.Add(new() { Role = role });
        await InitializeCreditsAsync(auction, teams, cancellationToken);
        database.AuctionSessions.Add(auction);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return auction.Id;
    }

    public async Task<AuctionStateDto> GetAsync(ClaimsPrincipal caller, Guid id, CancellationToken cancellationToken = default)
    {
        var auction = await SessionAsync(id, false, cancellationToken);
        var season = await competitions.AuthorizeAsync(caller, auction.GroupId, auction.LeagueId, auction.SeasonIndex, cancellationToken);
        var actor = await ActorAsync(caller, auction.GroupId, cancellationToken);
        var rules = await database.LeagueRules.AsNoTracking().SingleAsync(x => x.GroupId == auction.GroupId && x.LeagueId == auction.LeagueId && x.SeasonIndex == auction.SeasonIndex, cancellationToken);
        var teams = new List<AuctionTeamDto>();
        foreach (var team in await TeamsAsync(auction.GroupId, auction.LeagueId, auction.SeasonIndex, cancellationToken))
        {
            var owner = team.Owners.Single(x => x.IsPrimary).Membership.User.OwnerReference;
            var roster = await rosters.GetAsync(caller, auction.GroupId, team.BasketId, auction.SeasonIndex, owner, teamId: team.Id, cancellationToken: cancellationToken)
                ?? throw new ApplicationFault(409, "auction_roster_unavailable", "Una rosa dell'asta non è disponibile.");
            teams.Add(new(new(auction.GroupId, auction.SeasonIndex, team.BasketId, roster.Owner), roster));
        }
        var catalogue = await CatalogueAsync(caller, auction.SeasonIndex, cancellationToken);
        ActualAuctionDto? actual = null;
        if (auction.Actual is { } current)
        {
            var person = await database.Players.AsNoTracking().SingleAsync(x => x.Id == current.PlayerId, cancellationToken);
            var club = await database.Clubs.AsNoTracking().SingleAsync(x => x.Id == current.ClubId, cancellationToken);
            actual = new(Statistic(current, person, club), current.StartedAtUtc, current.TimerStartedAtUtc, current.WinningTeamId, current.OwnerEmail,
                current.SubstitutedPlayerId, current.SubstitutedPlayerId, current.Price);
        }
        var emails = await ConnectedUsersCoreAsync(auction, cancellationToken);
        var participants = teams.Where(team => emails.Any(email => team.Value.Owner == email || team.Value.AdditionalOwners.Contains(email)))
            .Select(team => new GameAnnualTeamDto(team.Value.Name, team.Value.Owner, team.Value.AdditionalOwners, team.Value.TeamId)).ToArray();
        return new(new(auction.Id, new(auction.GroupId, auction.LeagueId, auction.SeasonIndex), auction.CreatorEmail, auction.CreatedAtUtc, auction.Type, auction.Kind,
            actual, auction.Winnings.OrderBy(x => x.Ordinal).Select(x => new AuctionWinDto(x.PlayerId, x.PlayerId, x.TeamId, x.OwnerEmail, x.Price)).ToArray(),
            auction.Status, auction.PlayerOrder.GroupBy(x => x.Role).ToDictionary(x => x.Key, x => new AuctionRoleCountDto(x.Count(), x.Count(row => row.IsShown))), participants,
            auction.LastShown.ToDictionary(x => x.Role, x => x.PlayerId), auction.SecondsPerAuction, auction.CurrentRole, auction.Version),
            clock.GetUtcNow(), new(auction.SeasonIndex, season.CompetitionType, LeagueRulesMapping.ToDto(rules)), season.League.Name, teams.ToArray(), catalogue,
            IsAuctioneer(actor.Roles), actor.Email);
    }

    public async Task<StatisticPlayerDto[]> DiscardedAsync(ClaimsPrincipal caller, Guid id, int role, CancellationToken cancellationToken = default)
    {
        var auction = await SessionAsync(id, false, cancellationToken);
        await competitions.AuthorizeAsync(caller, auction.GroupId, auction.LeagueId, auction.SeasonIndex, cancellationToken);
        var shown = auction.PlayerOrder.Where(x => x.Role == role && x.IsShown).Select(x => x.PlayerId).ToHashSet();
        var removed = await database.RosterSnapshots.Where(x => x.GroupId == auction.GroupId && x.SeasonIndex == auction.SeasonIndex && x.Kind == RosterKind.Season &&
            database.LeagueBasketSeasons.Any(p => p.GroupId == auction.GroupId && p.LeagueId == auction.LeagueId && p.SeasonIndex == auction.SeasonIndex && p.BasketId == x.BasketId))
            .SelectMany(x => x.Players).Where(x => x.PlayerRole == role && (x.Status == 2 || x.Status == 4)).Select(x => x.PlayerId).ToArrayAsync(cancellationToken);
        shown.UnionWith(removed); shown.ExceptWith(auction.Winnings.Select(x => x.PlayerId));
        var explicitlyRemoved = await database.AuctionRosterChanges.Where(x => x.AuctionId == auction.Id && x.Status == 4).Select(x => x.PlayerId).ToArrayAsync(cancellationToken);
        shown.UnionWith(explicitlyRemoved); shown.ExceptWith(auction.Winnings.Select(x => x.PlayerId));
        return (await CatalogueAsync(caller, auction.SeasonIndex, cancellationToken)).Where(x => x.Role == role && shown.Contains(x.PlayerId)).ToArray();
    }

    private async Task<AuctionSession> SessionAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = database.AuctionSessions.Where(x => x.Id == id).AsSplitQuery();
        return await (tracking ? query : query.AsNoTracking()).SingleOrDefaultAsync(cancellationToken)
            ?? throw new ApplicationFault(404, "auction_not_found", "Asta non trovata.");
    }
    private async Task<(Guid Id, string Email, GroupRole Roles)> ActorAsync(ClaimsPrincipal caller, Guid group, CancellationToken cancellationToken)
    {
        var email = caller.FindFirstValue(ClaimTypes.Email)?.Trim().ToLowerInvariant();
        var membership = await database.GroupMemberships.AsNoTracking().Include(x => x.User).SingleOrDefaultAsync(x => x.GroupId == group && x.User.Email == email && x.User.IsEnabled && x.Roles != GroupRole.None, cancellationToken)
            ?? throw Denied();
        return (membership.UserId, membership.User.Email!, membership.Roles);
    }
    private Task<FantasyTeamSeason[]> TeamsAsync(Guid group, Guid league, int year, CancellationToken cancellationToken) => database.TeamSeasons.AsNoTracking()
        .Include(x => x.Owners).ThenInclude(x => x.Membership).ThenInclude(x => x.User).Where(x => x.GroupId == group && x.SeasonIndex == year && !x.IsArchived &&
            database.LeagueParticipations.Any(p => p.GroupId == group && p.LeagueId == league && p.SeasonIndex == year && p.TeamId == x.Id) &&
            !database.TeamDerivations.Any(d => d.GroupId == group && d.TeamId == x.Id)).AsSplitQuery().ToArrayAsync(cancellationToken);
    private async Task<StatisticPlayerDto[]> CatalogueAsync(ClaimsPrincipal caller, int year, CancellationToken cancellationToken)
    {
        var stats = (await football.StatisticsAsync(caller, year, cancellationToken))?.Players ?? [];
        var players = (await football.PlayersAsync(caller, year, cancellationToken))?.Players ?? [];
        return players.Where(x => x.RealTeam != null).Select(x => stats.SingleOrDefault(stat => stat.PlayerId == x.PlayerId) ?? new(x.PlayerId, x.Name, x.RealTeam, x.Role,
            x.IsActive, x.IsVisible, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, [], null)).ToArray();
    }
    private static StatisticPlayerDto Statistic(AuctionCurrentLot lot, FootballPlayer player, FootballClub club)
    {
        var stat = lot.Statistics;
        return new(player.Id, player.Name, FootballMapping.Club(club), lot.PlayerRole, lot.IsActive, lot.IsVisible, stat.Summatory, stat.FantaSummatory,
            stat.WithVote, stat.WithoutVote, stat.NoPlayed, stat.WithSpecial, stat.Goals, stat.Penalties, stat.Assists, stat.StoppedPenalties, stat.SufferedGoals,
            stat.WrongedPenalties, stat.OwnGoals, stat.YellowCards, stat.RedCards, stat.EnoughVotes, stat.ManOfTheMatch, stat.Injured,
            stat.Games.OrderBy(x => x.Ordinal).Select(x => new StatisticGameDto(x.Day, x.Vote, x.Positiveness)).ToArray(), stat.HasDescription ? new(stat.DescriptionTop, stat.DescriptionDown) : null);
    }
    private static bool IsAuctioneer(GroupRole role) => (role & (GroupRole.Admin | GroupRole.SuperAdmin)) != 0;
    private static ApplicationFault Invalid(string message) => new(400, "invalid_auction_request", message);
    private static ApplicationFault Denied() => new(403, "auction_access_denied", "Operazione non consentita per questa asta.");
}

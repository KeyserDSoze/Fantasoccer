using System.Security.Claims;
using System.Security.Cryptography;
using Fantasoccer.Application.Core.Football;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Application.Core.Competitions;

public sealed record PlayerOrderDto(FootballPlayerDto[] Players, int Version);
public sealed record RandomizePlayerOrderRequest(int? ExpectedVersion = null);

public sealed class PlayerOrderApplicationService(FantasoccerDbContext database, CompetitionApplicationService competitions)
{
    public async Task<PlayerOrderDto?> GetAsync(ClaimsPrincipal caller, Guid group, Guid league, int year, CancellationToken cancellationToken = default)
    {
        await competitions.AuthorizeAsync(caller, group, league, year, cancellationToken);
        var order = await database.LeaguePlayerOrders.AsNoTracking().Include(x => x.Players).ThenInclude(x => x.Player).Include(x => x.Players).ThenInclude(x => x.Club)
            .Where(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year).AsSplitQuery().SingleOrDefaultAsync(cancellationToken);
        if (order is null) return null;
        var keys = await database.PlayerSeasons.AsNoTracking().Where(x => x.SeasonIndex == year)
            .ToDictionaryAsync(x => x.PlayerId, x => x.PlayerKey, cancellationToken);
        return new(order.Players.OrderBy(x => x.Ordinal).Select(x => new FootballPlayerDto(x.PlayerId, x.Player.Name,
            FootballMapping.Club(x.Club), x.PlayerRole, x.IsActive, x.IsVisible) { SeasonIndex = year, PlayerKey = keys[x.PlayerId] }).ToArray(), order.Version);
    }
    public async Task RandomizeAsync(ClaimsPrincipal caller, Guid group, Guid league, int year, RandomizePlayerOrderRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        await competitions.AuthorizeManagerAsync(caller, group, league, year, cancellationToken);
        var old = await database.LeaguePlayerOrders.SingleOrDefaultAsync(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year, cancellationToken);
        if ((old?.Version ?? 0) != (request.ExpectedVersion ?? 0)) throw new ApplicationFault(409, "player_order_changed", "Ricaricare l'ordinamento prima di sostituirlo.");
        var players = await database.PlayerSeasons.AsNoTracking().Where(x => x.SeasonIndex == year && x.ClubId != null).ToArrayAsync(cancellationToken);
        if (players.Length == 0) throw new ApplicationFault(409, "player_catalog_unavailable", "Catalogo giocatori non disponibile.");
        if (old != null)
        {
            database.LeaguePlayerOrders.Remove(old);
            await database.SaveChangesAsync(cancellationToken);
        }
        var order = new LeaguePlayerOrder { GroupId = group, LeagueId = league, SeasonIndex = year, Version = (old?.Version ?? 0) + 1 };
        foreach (var player in players.OrderBy(x => x.Position).ThenBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)))
            order.Players.Add(new() { Ordinal = order.Players.Count, PlayerId = player.PlayerId, ClubId = player.ClubId!.Value,
                PlayerRole = (int)player.Position, IsActive = player.IsActive, IsVisible = false });
        database.LeaguePlayerOrders.Add(order);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}

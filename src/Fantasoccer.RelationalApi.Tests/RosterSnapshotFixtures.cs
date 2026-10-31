using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.RelationalApi.Tests;

internal static class RosterSnapshotFixtures
{
    // Test setup attests ownership before moving kickoff into the past. It does not create competition lineups.
    public static async Task FreezeAsync(PostgreSqlFixture fixture, Guid group, int year, int day)
    {
        await using var database = fixture.CreateContext();
        var sources = await database.RosterSnapshots.Where(x => x.GroupId == group && x.SeasonIndex == year && x.Kind == RosterKind.Season).AsSplitQuery().ToArrayAsync();
        foreach (var source in sources)
        {
            if (await database.RosterSnapshots.AnyAsync(x => x.GroupId == group && x.SeasonIndex == year && x.TeamId == source.TeamId && x.Kind == RosterKind.Matchday && x.Day == day)) continue;
            database.RosterSnapshots.Add(new() { GroupId = group, BasketId = source.BasketId, SeasonIndex = year, TeamId = source.TeamId, TeamOriginYear = source.TeamOriginYear,
                Kind = RosterKind.Matchday, Day = day, CompetitionId = Guid.Empty, OwnerUserId = source.OwnerUserId, Name = source.Name,
                MoneyFromRank = source.MoneyFromRank, OpeningCompetitionPrize = source.OpeningCompetitionPrize, MarketBalanceAdjustment = source.MarketBalanceAdjustment,
                RankCreditGranted = source.RankCreditGranted, LastUpdateUtc = DateTimeOffset.UtcNow,
                Players = source.Players.Select(p => new RosterPlayer { Ordinal = p.Ordinal, PlayerId = p.PlayerId, ClubId = p.ClubId, PlayerRole = p.PlayerRole,
                    IsActive = p.IsActive, IsVisible = p.IsVisible, Price = p.Price, Revenue = p.Revenue, Status = p.Status, Position = p.Position }).ToArray() });
        }
        await database.SaveChangesAsync();
    }
}

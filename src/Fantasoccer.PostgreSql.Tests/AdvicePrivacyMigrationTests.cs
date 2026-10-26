using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed partial class CoreMigrationTests
{
    [CoreFact]
    public async Task Advice_schema_upgrade_and_downgrade_refuse_to_guess_or_remove_private_recipients()
    {
        await using var database = fixture.CreateContext();
        var migrations = database.Database.GetMigrations().ToArray();
        var current = migrations.Single(x => x.EndsWith("AccountScopedTeamAdvice", StringComparison.Ordinal));
        var previous = migrations[Array.IndexOf(migrations, current) - 1];
        // The current importer requires the current schema. Seed core data
        // first, then downgrade only to exercise the legacy advice boundary.
        await ImportAsync();
        var migrator = database.GetService<IMigrator>(); await migrator.MigrateAsync(previous);
        var team = await database.TeamSeasons.AsNoTracking().OrderBy(x => x.Name).FirstAsync();
        var owner = await database.TeamOwnerships.Where(x => x.TeamId == team.Id && x.IsPrimary).Select(x => x.UserId).SingleAsync();
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "TeamAdvice" ("GroupId", "BasketId", "SeasonIndex", "TeamId", "Day", "Message", "IsGenerated", "Version")
            VALUES ({team.GroupId}, {team.BasketId}, {team.SeasonIndex}, {team.Id}, 1, 'Private legacy text', false, 1)
            """);
        var refused = await Assert.ThrowsAnyAsync<Exception>(() => migrator.MigrateAsync(current));
        Assert.Contains("lack a verifiable private recipient", refused.ToString());
        await database.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"TeamAdvice\" WHERE \"GroupId\" = {team.GroupId} AND \"TeamId\" = {team.Id} AND \"Day\" = 1");
        await migrator.MigrateAsync(current);
        database.TeamAdvice.Add(new() { GroupId = team.GroupId, BasketId = team.BasketId, TeamId = team.Id, OwnerUserId = owner, SeasonIndex = team.SeasonIndex, Day = 1, Message = "Private SQL text" });
        await database.SaveChangesAsync();
        var downgrade = await Assert.ThrowsAnyAsync<Exception>(() => migrator.MigrateAsync(previous));
        Assert.Contains("Cannot remove the private advice recipient", downgrade.ToString());
        Assert.Equal(owner, (await database.TeamAdvice.AsNoTracking().SingleAsync()).OwnerUserId);
    }
}

using Fantasoccer.DataMigration;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.StorageMigration;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed partial class CoreMigrationTests
{
    [CoreFact]
    public async Task Legacy_basket_links_become_explicit_participations_and_owners_are_materialized_without_inventing_derivations()
    {
        await ImportAsync();
        await using var database = fixture.CreateContext();
        var participations = await database.LeagueParticipations.AsNoTracking().ToArrayAsync();
        // "league" and "cup" both use the basket of season 15: one row each, legacy source.
        Assert.Equal(await database.LeagueBasketSeasons.CountAsync(), participations.Select(x => (x.LeagueId, x.BasketId, x.SeasonIndex)).Distinct().Count());
        Assert.All(participations, x => Assert.Equal(ParticipationSource.Legacy, x.Source));
        Assert.Equal(participations.Length, await database.LeagueParticipations.Join(database.TeamSeasons, p => p.TeamId, t => t.Id, (p, t) => t.BasketId == p.BasketId).CountAsync(x => x));
        var owners = await database.LeagueParticipantOwners.AsNoTracking().ToArrayAsync();
        var expected = await database.LeagueParticipations.Join(database.TeamOwnerships, p => new { p.GroupId, p.TeamId }, o => new { o.GroupId, o.TeamId },
            (p, o) => new { p.LeagueId, p.SeasonIndex, o.UserId }).Distinct().CountAsync();
        Assert.Equal(expected, owners.Length);
        Assert.False(await database.TeamDerivations.AnyAsync());
        Assert.False(await database.BasketDerivations.AnyAsync());
        Assert.All(await database.Baskets.ToArrayAsync(), x => Assert.Equal(BasketKind.Autonomous, x.Kind));
        Assert.All(await database.Leagues.ToArrayAsync(), x => Assert.Equal(LeagueMode.Autonomous, x.Mode));
        // A re-import of the same dump rewrites the same participations idempotently.
        var report = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(report.Documents, report.SkippedDocuments);
    }

    [CoreFact]
    public async Task Legacy_co_owner_and_owner_of_two_teams_in_the_same_league_is_refused_for_explicit_reconciliation()
    {
        // other@ is co-owner of "Team" and owner of "Second team": both play "league".
        _first.Baskets[0].Years[0].Teams.Add(new() { Name = "Second team", Owner = OtherEmail, AdditionalOwners = [] });
        await PutGroupAsync(_first);
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var fault = await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump));
        Assert.Equal("league_owner_conflict", fault.Message);
        await using var database = fixture.CreateContext();
        Assert.False(await database.Groups.AnyAsync());
    }
}

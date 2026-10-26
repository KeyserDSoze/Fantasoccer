using System.Text.Json;
using Fantasoccer.Application.Core.Cards;
using Fantasoccer.DataMigration;
using Fantasoccer.StorageMigration;
using Microsoft.EntityFrameworkCore;
using RepositoryFramework;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed partial class CoreMigrationTests
{
    [CoreFact]
    public async Task Card_import_preserves_empty_bounds_false_zero_duplicate_roles_catalogue_scope_and_blob_identity()
    {
        var container = await ExtraContainerAsync("cards");
        var image = await ExtraContainerAsync("cardimages");
        await image.GetBlobClient("same-local-card").UploadAsync(BinaryData.FromBytes(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }));
        var card = new Card { Id = "same-local-card", Name = "Rule card", Description = "Preserved rule", Type = CardType.Strategy,
            Points = 3.25f, NegativePoints = -1.5f, EnemyPoints = 2.5f,
            MyCondition = new() { RolesInvolved = [FantaSoccerRole.All, FantaSoccerRole.GoalKeeper, FantaSoccerRole.All],
                ConditionForPlayer = new() { MinimumNumberOfOccurencies = 0, Minimum = new(), Maximum = new() { Vote = 7.5f, HasRed = false, Goals = 0 } },
                Condition = new() { Minimum = new(), Maximum = new() { Goal = 3, WithoutVote = 0, Injured = 1 } } },
            EnemyCondition = new() { RolesInvolved = [] } };
        foreach (var library in new[] { "default", "unclassified-private-library" })
            await container.GetBlobClient(library).UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<CardWrapper, string>.Default(new() { Cards = [card] }, library))));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await ImportAsync();
        await using var database = fixture.CreateContext();
        Assert.Equal(2, await database.CardLibraries.CountAsync());
        var cards = await database.RuleCards.AsSplitQuery().ToArrayAsync();
        Assert.Equal(2, cards.Select(x => x.Id).Distinct().Count());
        var saved = cards.Single(x => x.Library == "default");
        Assert.Equal("same-local-card", saved.ImageBlobName);
        Assert.Equal(-1.5f, saved.NegativePoints);
        Assert.Equal(new[] { -2, 0, -2 }, saved.MyCondition!.Roles.OrderBy(x => x.Ordinal).Select(x => x.Role));
        Assert.NotNull(saved.MyCondition.MinimumPlayer);
        Assert.Null(saved.MyCondition.MinimumPlayer.Vote);
        Assert.False(saved.MyCondition.MaximumPlayer!.HasRed);
        Assert.Equal(0, saved.MyCondition.MaximumPlayer.Goals);
        Assert.NotNull(saved.MyCondition.MinimumTeam);
        Assert.NotNull(saved.EnemyCondition);
        Assert.Empty(saved.EnemyCondition.Roles);
        var dto = CardRulesMapping.ToDto(saved.MyCondition)!;
        Assert.NotNull(dto.ConditionForPlayer!.Minimum);
        Assert.NotNull(dto.Condition!.Minimum);
        var domain = CardRulesMapping.ToDomain(saved);
        Assert.Equal(3, domain.MyCondition!.Condition!.Maximum!.Goal);
        Assert.Equal(-1.5f, domain.NegativePoints);
        var id = saved.Id;
        var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
        await container.DeleteBlobAsync("unclassified-private-library");
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(id, (await database.RuleCards.SingleAsync()).Id);
    }
}

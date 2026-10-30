using System.Text.Json;
using Fantasoccer.DataMigration;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.StorageMigration;
using Microsoft.EntityFrameworkCore;
using Fantasoccer.LegacyModels;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed partial class CoreMigrationTests
{
    [CoreFact]
    public async Task Push_checkpoint_import_preserves_baselines_receipts_attempts_tags_deadlines_and_quarantines_legacy_deliveries()
    {
        var container = await ExtraContainerAsync("pushnotificationsent");
        var expires = new DateTimeOffset(2030, 5, 10, 12, 0, 0, TimeSpan.FromHours(2));
        var checkpoint = new PushNotificationSent { DeploymentReminder = true, LiveChangesReminder = true, MatchEventSequence = 13,
            LiveFormationChangeNotifications = ["first:basket:owner:0", "first:basket:owner:0"], MatchEventVotes = new() { Players =
                [new() { Name = "Checkpoint player", Role = Role.GoalKeeper, RealTeam = new() { Name = "Checkpoint club" }, Vote = new() { Value = 7.5f, Goal = 1, HasVote = true } }] },
            PendingNotifications = [new() { Id = "legacy-delivery", Email = Email, Kind = PushNotificationKind.MatchEvents, Attempts = 4,
                NextAttemptAt = expires.AddMinutes(-5), ExpiresAt = expires, CompletedEndpoints = ["https://fcm.googleapis.com/fcm/send/completed"], Notification = new()
                { Title = "Saved title", Body = "Saved body", Tag = "saved-stable-tag", RequireInteraction = true, Data = new() { Uri = "https://fanta.zone/live", RecipientEmail = Email } } }] };
        await container.GetBlobClient("day").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(LegacyEntity<PushNotificationSent, YearDayKey>.Default(checkpoint, new() { Year = 15, Day = 3 }))));
        await container.GetBlobClient("index").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(LegacyEntity<PushNotificationSent, YearDayKey>.Default(new()
            { ActiveOutboxDays = [new() { Year = 15, Day = 3 }, new() { Year = 15, Day = 0 }] }, new() { Year = 0, Day = 0 }))));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await ImportAsync();
        await using var database = fixture.CreateContext();
        var saved = await database.PushDayCheckpoints.AsSplitQuery().SingleAsync();
        Assert.True(saved.DeploymentReminder); Assert.True(saved.LiveChangesReminder); Assert.False(saved.EndDay);
        Assert.Equal(13, saved.MatchEventSequence);
        Assert.Equal(2, saved.FormationReceipts.Count);
        var vote = Assert.Single(saved.Votes);
        Assert.Equal(7.5f, vote.Value); Assert.Equal(1, vote.Goals);
        Assert.Equal(2, await database.LegacyPushActiveDays.CountAsync());
        var pending = await database.PushDeliveries.SingleAsync();
        Assert.Equal(PushDeliveryStatus.Quarantined, pending.Status);
        Assert.Null(pending.GroupId); Assert.Null(pending.LeaseId);
        Assert.Equal(4, pending.Attempts); Assert.Equal(expires.ToUniversalTime(), pending.ExpiresAtUtc);
        Assert.Equal("saved-stable-tag", pending.Tag);
        Assert.Equal("https://fcm.googleapis.com/fcm/send/completed", Assert.Single(pending.Receipts).LegacyEndpoint);
        var id = pending.Id;
        var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
        checkpoint.PendingNotifications[0].Attempts = 5;
        await container.GetBlobClient("day").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(LegacyEntity<PushNotificationSent, YearDayKey>.Default(checkpoint, new() { Year = 15, Day = 3 }))), overwrite: true);
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(id, (await database.PushDeliveries.SingleAsync()).Id);
        Assert.Equal(5, (await database.PushDeliveries.SingleAsync()).Attempts);
        await container.DeleteBlobAsync("day"); await container.DeleteBlobAsync("index");
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Empty(await database.PushDeliveries.ToArrayAsync()); Assert.Empty(await database.PushDayCheckpoints.ToArrayAsync());
        Assert.Empty(await database.LegacyPushActiveDays.ToArrayAsync());
    }
}

using System.Security.Cryptography;
using System.Text.Json;
using Fantasoccer.Business;
using Fantasoccer.DataMigration;
using Fantasoccer.StorageMigration;
using Microsoft.EntityFrameworkCore;
using RepositoryFramework;
using WebPush;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed partial class CoreMigrationTests
{
    private static PushSubscription PushDevice(string id)
    {
        using var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var point = key.ExportParameters(false).Q;
        return new($"https://fcm.googleapis.com/fcm/send/{id}", Convert.ToBase64String(new byte[] { 4 }.Concat(point.X!).Concat(point.Y!).ToArray()), Convert.ToBase64String(new byte[16]));
    }

    [CoreFact]
    public async Task Push_preferences_preserve_optouts_original_subscription_rows_and_validity_without_device_capabilities_or_sends()
    {
        var container = await ExtraContainerAsync("pushnotification");
        var valid = PushDevice("migration");
        var settings = new PushNotificationSettings { MatchEvents = false, MarketEvents = true,
            Subscriptions = [valid, valid, new("https://127.0.0.1/private", "bad", "bad")] };
        await container.GetBlobClient("sample").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<PushNotificationSettings, string>.Default(settings, Email))));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await ImportAsync();
        await using var database = fixture.CreateContext();
        var preferences = await database.UserPushPreferences.SingleAsync();
        Assert.False(preferences.MatchEvents);
        Assert.True(preferences.MarketEvents);
        Assert.Equal(3, preferences.LegacySubscriptions.Count);
        Assert.Equal(2, await database.PushDeviceBindings.CountAsync());
        var binding = await database.PushDeviceBindings.SingleAsync(x => x.EndpointHash == PushEndpointIdentity.Hash(valid.Endpoint));
        Assert.True(binding.IsValid);
        Assert.Null(binding.DeviceProofHash);
        Assert.False((await database.PushDeviceBindings.SingleAsync(x => x.EndpointHash != binding.EndpointHash)).IsValid);
        Assert.Empty(await database.PushDeviceOwnershipEvents.ToArrayAsync());
        var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
        await container.DeleteBlobAsync("sample");
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Empty(await database.PushDeviceBindings.ToArrayAsync());
        Assert.Empty(await database.UserPushPreferences.ToArrayAsync());
    }

    [CoreFact]
    public async Task Push_endpoint_collision_between_source_accounts_aborts_before_any_import_write()
    {
        var container = await ExtraContainerAsync("pushnotification");
        var device = PushDevice("collision");
        foreach (var email in new[] { Email, OtherEmail })
            await container.GetBlobClient(email).UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<PushNotificationSettings, string>.Default(new() { Subscriptions = [device] }, email))));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var error = await Assert.ThrowsAsync<CoreImportException>(() => ImportAsync());
        Assert.Equal("push_endpoint_owner_collision", error.Message);
        await using var database = fixture.CreateContext();
        Assert.Empty(await database.PushDeviceBindings.ToArrayAsync());
    }
}

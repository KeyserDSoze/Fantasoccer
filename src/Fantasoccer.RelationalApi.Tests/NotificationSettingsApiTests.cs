using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Notifications;
using Fantasoccer.Business;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Fantasoccer.PostgreSql.Tests;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed partial class WorkspaceApiTests
{
    private static PushSubscriptionDto Subscription(string id)
    {
        using var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var point = key.ExportParameters(false).Q;
        return new($"https://fcm.googleapis.com/fcm/send/{id}", Convert.ToBase64String(new byte[] { 4 }.Concat(point.X!).Concat(point.Y!).ToArray()), Convert.ToBase64String(new byte[16]));
    }

    [PostgreSqlFact]
    public async Task Push_HTTP_is_account_scoped_keeps_optouts_and_transfers_devices_only_with_rotating_private_proofs()
    {
        using var first = await factory.AccountAsync();
        using var second = await factory.AccountAsync();
        var account = (await first.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var path = "/api/push-notifications";
        Assert.Equal(HttpStatusCode.Forbidden, (await second.GetAsync(path + "?email=" + Uri.EscapeDataString(account.Email))).StatusCode);
        (await first.PatchAsJsonAsync(path, new PushPreferencesPatch(MatchEvents: false, MarketEvents: true))).EnsureSuccessStatusCode();
        var subscription = Subscription(Guid.NewGuid().ToString("N"));
        var response = await first.PostAsJsonAsync(path + "/devices", new RegisterNotificationDeviceRequest(subscription));
        response.EnsureSuccessStatusCode();
        var registration = (await response.Content.ReadFromJsonAsync<RegisteredNotificationDeviceDto>())!;
        Assert.False(registration.Settings.MatchEvents);
        Assert.True(registration.Settings.MarketEvents);
        Assert.NotEmpty(registration.DeviceProof);
        var json = await first.GetFromJsonAsync<JsonElement>(path);
        Assert.False(json.TryGetProperty("deviceProof", out _));
        Assert.False(json.TryGetProperty("privateKey", out _));
        Assert.Equal(HttpStatusCode.Conflict, (await second.PostAsJsonAsync(path + "/devices", new RegisterNotificationDeviceRequest(subscription))).StatusCode);
        (await second.PostAsJsonAsync(path + "/devices/remove", new RemoveNotificationDeviceRequest(subscription.Endpoint))).EnsureSuccessStatusCode();
        Assert.Single((await first.GetFromJsonAsync<PushSettingsDto>(path))!.Subscriptions);
        var moved = await second.PostAsJsonAsync(path + "/devices", new RegisterNotificationDeviceRequest(subscription, DeviceProof: registration.DeviceProof));
        moved.EnsureSuccessStatusCode();
        var transferred = (await moved.Content.ReadFromJsonAsync<RegisteredNotificationDeviceDto>())!;
        Assert.NotEqual(registration.DeviceProof, transferred.DeviceProof);
        Assert.Empty((await first.GetFromJsonAsync<PushSettingsDto>(path))!.Subscriptions);
        Assert.Equal(HttpStatusCode.Conflict, (await first.PostAsJsonAsync(path + "/devices", new RegisterNotificationDeviceRequest(subscription, DeviceProof: registration.DeviceProof))).StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<NotificationSettingsApplicationService>().RemoveInvalidAsync(PushEndpointIdentity.Hash(subscription.Endpoint), account.Id, registration.Settings.Subscriptions[0].Version, default);
        Assert.Single((await second.GetFromJsonAsync<PushSettingsDto>(path))!.Subscriptions);
        var newKey = Subscription(Guid.NewGuid().ToString("N"));
        (await second.PostAsJsonAsync(path + "/devices", new RegisterNotificationDeviceRequest(newKey, subscription.Endpoint))).EnsureSuccessStatusCode();
        Assert.Equal(newKey.Endpoint, Assert.Single((await second.GetFromJsonAsync<PushSettingsDto>(path))!.Subscriptions).Endpoint);
        (await second.PostAsync(path + "/disable", null)).EnsureSuccessStatusCode();
        var disabled = (await second.GetFromJsonAsync<PushSettingsDto>(path))!;
        Assert.False(disabled.MatchEvents);
        Assert.Empty(disabled.Subscriptions);
        Assert.Equal(HttpStatusCode.BadRequest, (await second.PostAsJsonAsync(path + "/devices", new RegisterNotificationDeviceRequest(subscription with { Endpoint = "https://localhost/private" }))).StatusCode);
    }

    [PostgreSqlFact]
    public async Task Imported_devices_require_reactivation_for_other_accounts_and_stale_cleanup_cannot_erase_same_account_refresh()
    {
        using var first = await factory.AccountAsync();
        using var second = await factory.AccountAsync();
        var account = (await first.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var subscription = Subscription(Guid.NewGuid().ToString("N"));
        var hash = PushEndpointIdentity.Hash(subscription.Endpoint);
        await using (var database = factory.Database.CreateContext())
        {
            database.PushDeviceBindings.Add(new() { EndpointHash = hash, UserId = account.Id, Endpoint = subscription.Endpoint, P256dh = subscription.P256dh, Auth = subscription.Auth, IsValid = true });
            await database.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict, (await second.PostAsJsonAsync("/api/push-notifications/devices", new RegisterNotificationDeviceRequest(subscription))).StatusCode);
        (await first.PostAsJsonAsync("/api/push-notifications/devices", new RegisterNotificationDeviceRequest(subscription))).EnsureSuccessStatusCode();
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<NotificationSettingsApplicationService>().RemoveInvalidAsync(hash, account.Id, 1, default);
        Assert.Single((await first.GetFromJsonAsync<PushSettingsDto>("/api/push-notifications"))!.Subscriptions);
    }
}

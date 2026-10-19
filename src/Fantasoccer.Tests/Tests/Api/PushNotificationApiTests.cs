using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Fantasoccer.Business;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;
using WebPush;

namespace Fantasoccer.Tests.Tests.Api;

[Collection("API integration")]
public sealed class PushNotificationApiTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private HttpClient Client(string? email = "push-api@test.local")
    {
        var client = factory.CreateClient();
        if (email != null) client.DefaultRequestHeaders.Add("X-Test-User", email);
        return client;
    }
    private IRepository<PushNotificationSettings, string> Repository => factory.Services.GetRequiredService<IRepository<PushNotificationSettings, string>>();
    private static PushSubscription Subscription(string id = "device")
    {
        using var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var p = ecdh.ExportParameters(false);
        var publicKey = new byte[] { 4 }.Concat(p.Q.X!).Concat(p.Q.Y!).ToArray();
        return new($"https://fcm.googleapis.com/fcm/send/test-{id}", Convert.ToBase64String(publicKey), Convert.ToBase64String(new byte[16]));
    }

    [Theory]
    [InlineData("GET", "")]
    [InlineData("PATCH", "")]
    [InlineData("POST", "/devices")]
    [InlineData("POST", "/devices/remove")]
    [InlineData("POST", "/disable")]
    public async Task DedicatedApiRequiresAuthentication(string method, string path)
    {
        using var client = Client(null);
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/push-notifications{path}") { Content = JsonContent.Create(new { }) };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request, Ct)).StatusCode);
    }

    [Fact]
    public async Task AnotherUsersSettingsCannotBeReadOrChanged()
    {
        const string victim = "push-victim@test.local";
        await Repository.InsertAsync(victim, new PushNotificationSettings(), Ct);
        try
        {
            using var client = Client();
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/PushNotificationSettings/Get?key={victim}", Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/PushNotificationSettings/Update?key={victim}", new { matchEvents = false }, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/push-notifications?email={victim}", Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PatchAsJsonAsync($"/api/push-notifications?email={victim}", new { matchEvents = false }, Ct)).StatusCode);
            Assert.True((await Repository.GetAsync(victim, Ct))!.MatchEvents);
        }
        finally { await Repository.DeleteAsync(victim, Ct); }
    }

    [Theory]
    [InlineData("Query")]
    [InlineData("Batch")]
    [InlineData("Operation")]
    public async Task RawRepositoryCannotBypassUserIsolation(string method)
    {
        using var client = Client();
        var result = await client.PostAsJsonAsync($"/api/PushNotificationSettings/{method}", new { }, Ct);
        Assert.True(result.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound, $"Unexpected {result.StatusCode} for {method}");
    }

    [Fact]
    public async Task InternalOutboxIsNotExposed()
    {
        using var client = Client();
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/PushNotificationSent/Query", new { }, Ct)).StatusCode);
    }

    [Fact]
    public async Task PartialUpdatesAndDeviceUpsertsPreserveUnrelatedState()
    {
        var email = $"push-partial-{Guid.NewGuid():N}@test.local";
        using var client = Client(email);
        var sub = Subscription();
        try
        {
            (await client.PostAsJsonAsync("/api/push-notifications/devices", new { subscription = sub }, Ct)).EnsureSuccessStatusCode();
            (await client.PatchAsJsonAsync("/api/push-notifications", new { matchEvents = false, subscriptions = Array.Empty<object>() }, Ct)).EnsureSuccessStatusCode();
            await Task.WhenAll(
                client.PatchAsJsonAsync("/api/push-notifications", new { marketEvents = true }, Ct),
                client.PatchAsJsonAsync("/api/push-notifications", new { endDay = true }, Ct));
            var saved = (await Repository.GetAsync(email, Ct))!;
            Assert.False(saved.MatchEvents);
            Assert.True(saved.MarketEvents);
            Assert.True(saved.EndDay);
            Assert.Single(saved.Subscriptions);
            var refreshed = Subscription();
            (await client.PostAsJsonAsync("/api/push-notifications/devices", new { subscription = refreshed }, Ct)).EnsureSuccessStatusCode();
            saved = (await Repository.GetAsync(email, Ct))!;
            Assert.Equal(refreshed.P256DH, Assert.Single(saved.Subscriptions).P256DH);
            Assert.False(saved.MatchEvents);
        }
        finally { await Repository.DeleteAsync(email, Ct); }
    }

    [Fact]
    public async Task SubscriptionRenewalReplacesOldEndpointAndTransfersOnlyTheActivatedDevice()
    {
        var first = $"push-first-{Guid.NewGuid():N}@test.local";
        var second = $"push-second-{Guid.NewGuid():N}@test.local";
        var old = Subscription(Guid.NewGuid().ToString());
        var renewed = Subscription(Guid.NewGuid().ToString());
        try
        {
            using var a = Client(first);
            using var b = Client(second);
            (await a.PostAsJsonAsync("/api/push-notifications/devices", new { subscription = old }, Ct)).EnsureSuccessStatusCode();
            (await a.PostAsJsonAsync("/api/push-notifications/devices", new { subscription = renewed, previousEndpoint = old.Endpoint }, Ct)).EnsureSuccessStatusCode();
            Assert.Equal(renewed.Endpoint, Assert.Single((await Repository.GetAsync(first, Ct))!.Subscriptions).Endpoint);
            (await b.PostAsJsonAsync("/api/push-notifications/devices", new { subscription = renewed }, Ct)).EnsureSuccessStatusCode();
            Assert.Empty((await Repository.GetAsync(first, Ct))!.Subscriptions);
            Assert.Single((await Repository.GetAsync(second, Ct))!.Subscriptions);
        }
        finally { await Repository.DeleteAsync(first, Ct); await Repository.DeleteAsync(second, Ct); }
    }

    [Fact]
    public async Task RegistrationRejectsNonProviderUrlsAndInvalidEncryptionKeys()
    {
        using var client = Client();
        foreach (var endpoint in new[] { "http://fcm.googleapis.com/test", "https://localhost/test", "https://127.0.0.1/test", "https://internal.example/test", "https://fcm.googleapis.com.attacker.example/test" })
        {
            var sub = Subscription();
            sub.Endpoint = endpoint;
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/push-notifications/devices", new { subscription = sub }, Ct)).StatusCode);
        }
        var invalid = Subscription();
        invalid.P256DH = Convert.ToBase64String(new byte[] { 4 }.Concat(new byte[64]).ToArray());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/push-notifications/devices", new { subscription = invalid }, Ct)).StatusCode);
    }

    [Fact]
    public async Task LoginEmailCasingDoesNotCreateANewDocumentOrLoseOptOuts()
    {
        var email = $"push-Cased-{Guid.NewGuid():N}@test.local";
        var service = factory.Services.GetRequiredService<PushNotificationSettingsService>();
        await Repository.InsertAsync(email, new PushNotificationSettings { MatchEvents = false }, Ct);
        try
        {
            using var client = Client(email.ToUpperInvariant());
            var read = await client.GetFromJsonAsync<PushNotificationSettings>("/api/push-notifications", Ct);
            Assert.False(read!.MatchEvents);
            (await client.PatchAsJsonAsync("/api/push-notifications", new { endDay = true }, Ct)).EnsureSuccessStatusCode();
            var saved = await Repository.GetAsync(email, Ct);
            Assert.False(saved!.MatchEvents);
            Assert.True(saved.EndDay);
            Assert.Single((await Repository.ToListAsync(cancellationToken: Ct)).Where(s => string.Equals(s.Key, email, StringComparison.OrdinalIgnoreCase)));
        }
        finally { await Repository.DeleteAsync(email, Ct); }
    }

    [Fact]
    public async Task StaleInvalidSubscriptionCleanupDoesNotEraseRefreshedKeysOrPreferences()
    {
        var email = $"push-cleanup-{Guid.NewGuid():N}@test.local";
        var service = factory.Services.GetRequiredService<PushNotificationSettingsService>();
        var old = Subscription();
        var refreshed = Subscription();
        try
        {
            await service.AddDeviceAsync(email, old, cancellationToken: Ct);
            await service.AddDeviceAsync(email, refreshed, cancellationToken: Ct);
            await service.UpdateAsync(email, new PushNotificationPreferences { EndDay = true, MatchEvents = false }, Ct);
            await service.RemoveInvalidAsync(email, old, Ct);
            var saved = await service.GetAsync(email, Ct);
            Assert.Equal(refreshed.P256DH, Assert.Single(saved.Subscriptions).P256DH);
            Assert.True(saved.EndDay);
            Assert.False(saved.MatchEvents);
        }
        finally { await Repository.DeleteAsync(email, Ct); }
    }
}

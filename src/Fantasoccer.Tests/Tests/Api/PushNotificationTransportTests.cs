using System.Net;
using System.Security.Cryptography;
using Fantasoccer.Business;
using Fantasoccer.Tests.Tests.Services;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;
using WebPush;

namespace Fantasoccer.Tests.Tests.Api;

[Collection("API integration")]
public sealed class PushNotificationTransportTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static PushSubscription Device(string id)
    {
        using var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var point = key.ExportParameters(false).Q;
        return new($"https://fcm.googleapis.com/fcm/send/{id}", Convert.ToBase64String(new byte[] { 4 }.Concat(point.X!).Concat(point.Y!).ToArray()), Convert.ToBase64String(new byte[16]));
    }

    [Theory]
    [InlineData(404)]
    [InlineData(410)]
    public async Task InvalidEndpointsAreRemovedUsingStatusAndNotRetried(int status)
    {
        var email = $"push-transport-{Guid.NewGuid():N}@test.local";
        var settingsService = factory.Services.GetRequiredService<PushNotificationSettingsService>();
        var repo = factory.Services.GetRequiredService<IRepository<PushNotificationSettings, string>>();
        var handler = factory.Services.GetRequiredService<TestWebPushHandler>();
        var sender = factory.Services.GetRequiredService<IPushNotificationService>();
        handler.Reset();
        handler.Responses.Enqueue((HttpStatusCode)status);
        try
        {
            var settings = await settingsService.AddDeviceAsync(email, Device("expired"), cancellationToken: Ct);
            var result = await sender.SendNotificationAsync(email, settings, new Notification { Title = "Test", Body = "Test", Data = new() { Uri = "https://fanta.zone/live" } });
            Assert.True(result.IsComplete);
            Assert.Single(handler.Requests);
            Assert.Empty((await settingsService.GetAsync(email, Ct)).Subscriptions);
            Assert.Equal(1, sender.GetStatistics().TotalDiscarded);
            Assert.Equal(0, sender.GetStatistics().TotalFailed);
        }
        finally { await repo.DeleteAsync(email, Ct); handler.Reset(); }
    }

    [Fact]
    public async Task PermanentFailureIsCountedOnceAndRemainsRetryableByOutbox()
    {
        var email = $"push-permanent-{Guid.NewGuid():N}@test.local";
        var settingsService = factory.Services.GetRequiredService<PushNotificationSettingsService>();
        var repo = factory.Services.GetRequiredService<IRepository<PushNotificationSettings, string>>();
        var handler = factory.Services.GetRequiredService<TestWebPushHandler>();
        var sender = factory.Services.GetRequiredService<IPushNotificationService>();
        handler.Reset();
        handler.Responses.Enqueue(HttpStatusCode.Forbidden);
        try
        {
            var settings = await settingsService.AddDeviceAsync(email, Device("forbidden"), cancellationToken: Ct);
            var result = await sender.SendNotificationAsync(email, settings, new Notification { Title = "Test", Body = "Test", Data = new() { Uri = "https://fanta.zone/live" } });
            Assert.False(result.IsComplete);
            Assert.Single(result.RetryEndpoints);
            Assert.Single(handler.Requests);
            Assert.Equal(1, sender.GetStatistics().TotalFailed);
            Assert.Single((await settingsService.GetAsync(email, Ct)).Subscriptions);
        }
        finally { await repo.DeleteAsync(email, Ct); handler.Reset(); }
    }

    [Fact]
    public async Task TransientFailureRetriesThenSucceedsWithoutMutatingCallerSettings()
    {
        var email = $"push-retry-{Guid.NewGuid():N}@test.local";
        var settingsService = factory.Services.GetRequiredService<PushNotificationSettingsService>();
        var repo = factory.Services.GetRequiredService<IRepository<PushNotificationSettings, string>>();
        var handler = factory.Services.GetRequiredService<TestWebPushHandler>();
        var sender = factory.Services.GetRequiredService<IPushNotificationService>();
        handler.Reset();
        handler.Responses.Enqueue(HttpStatusCode.ServiceUnavailable);
        handler.Responses.Enqueue(HttpStatusCode.Created);
        try
        {
            var settings = await settingsService.AddDeviceAsync(email, Device("retry"), cancellationToken: Ct);
            var result = await sender.SendNotificationAsync(email, settings, new Notification { Title = "Test", Body = "Test", Data = new() { Uri = "https://fanta.zone/live" } });
            Assert.True(result.IsComplete);
            Assert.Equal(2, handler.Requests.Count);
            Assert.Single(settings.Subscriptions);
            Assert.Equal(1, sender.GetStatistics().TotalSucceeded);
            Assert.Equal(0, sender.GetStatistics().TotalFailed);
        }
        finally { await repo.DeleteAsync(email, Ct); handler.Reset(); }
    }
}

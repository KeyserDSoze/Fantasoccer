using System.Net;
using Fantasoccer.Application.Api.Notifications;
using Fantasoccer.Application.Core.Notifications;
using Microsoft.Extensions.Options;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed class PushTransportTests
{
    [Fact]
    public async Task Configured_transport_encrypts_readable_payload_sets_TTL_and_distinguishes_expiry_from_retryable_VAPID_errors()
    {
        var keys = WebPush.VapidHelper.GenerateVapidKeys();
        var options = new PushTransportOptions { Enabled = true, PublicKey = keys.PublicKey, PrivateKey = keys.PrivateKey, Subject = "mailto:push-tests@example.test" };
        Assert.True(options.HasValidConfiguration());
        Assert.False(new PushTransportOptions { PublicKey = keys.PublicKey, PrivateKey = "invalid", Subject = options.Subject }.HasValidConfiguration());
        using var key = System.Security.Cryptography.ECDiffieHellman.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        var point = key.ExportParameters(false).Q;
        var device = new PushSubscriptionDto("https://fcm.googleapis.com/fcm/send/synthetic", Convert.ToBase64String(new byte[] { 4 }.Concat(point.X!).Concat(point.Y!).ToArray()), Convert.ToBase64String(new byte[16]));
        var payload = new PushPayload("Private title", "Private body", new("/live", "recipient@example.test"), null, null, "stable-tag", false);
        foreach (var (status, expected) in new[] { (HttpStatusCode.Created, PushTransportOutcome.Delivered), (HttpStatusCode.Gone, PushTransportOutcome.Gone),
            (HttpStatusCode.NotFound, PushTransportOutcome.Gone), (HttpStatusCode.Forbidden, PushTransportOutcome.Retry), (HttpStatusCode.ServiceUnavailable, PushTransportOutcome.Retry),
            (HttpStatusCode.TooManyRequests, PushTransportOutcome.Retry), (HttpStatusCode.Redirect, PushTransportOutcome.Retry), (HttpStatusCode.BadRequest, PushTransportOutcome.Rejected) })
        {
            var handler = new StubProvider(async request =>
            {
                Assert.Equal("120", Assert.Single(request.Headers.GetValues("TTL")));
                var bytes = await request.Content!.ReadAsByteArrayAsync();
                Assert.DoesNotContain("Private body", System.Text.Encoding.UTF8.GetString(bytes));
                return new HttpResponseMessage(status) { Content = new StringContent("") };
            });
            var transport = new WebPushDeliveryTransport(new ProviderClients(handler), Options.Create(options));
            Assert.Equal(expected, await transport.SendAsync(device, payload, 120, default));
        }
    }
    private sealed class ProviderClients(HttpMessageHandler handler) : IHttpClientFactory
    { public HttpClient CreateClient(string name) => new(handler, disposeHandler: false); }
    private sealed class StubProvider(Func<HttpRequestMessage, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => callback(request); }
}

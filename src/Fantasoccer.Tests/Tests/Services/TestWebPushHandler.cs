using System.Collections.Concurrent;
using System.Net;

namespace Fantasoccer.Tests.Tests.Services;

public sealed class TestWebPushHandler : HttpMessageHandler
{
    public ConcurrentQueue<HttpStatusCode> Responses { get; } = new();
    public ConcurrentQueue<string> Requests { get; } = new();
    public Action? BeforeResponse { get; set; }
    public void Reset()
    {
        Responses.Clear();
        Requests.Clear();
        BeforeResponse = null;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Enqueue(request.RequestUri!.AbsoluteUri);
        BeforeResponse?.Invoke();
        var status = Responses.TryDequeue(out var queued) ? queued : HttpStatusCode.Created;
        return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("Simulated provider response") });
    }
}

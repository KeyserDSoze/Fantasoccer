using System.Net;
using System.Net.Http.Json;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Application.Core.Notifications;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed partial class WorkspaceApiTests
{
    [PostgreSqlFact]
    public async Task Push_dispatch_uses_SQL_leases_endpoint_receipts_current_optouts_and_stable_tags_without_resending_completed_endpoints()
    {
        using var owner = await factory.AccountAsync(globalAdmin: true);
        var account = (await owner.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var group = await CreateGroupAsync(owner);
        var year = FantacalcioTime.ActualYear;
        var league = await CreateLeagueAsync(owner, group.GroupId, year);
        var graph = (await owner.GetFromJsonAsync<GameGroupDto>($"/api/groups/{group.GroupId}"))!;
        (await owner.PostAsJsonAsync($"/api/workspace/groups/{group.GroupId}/teams", new CreateTeamRequest(graph.Baskets[0].Id, year, "Dispatch team", account.Id, []))).EnsureSuccessStatusCode();
        var first = Subscription(Guid.NewGuid().ToString("N")); var second = Subscription(Guid.NewGuid().ToString("N"));
        foreach (var subscription in new[] { first, second })
            (await owner.PostAsJsonAsync("/api/push-notifications/devices", new RegisterNotificationDeviceRequest(subscription))).EnsureSuccessStatusCode();
        var clock = new PushTestClock();
        var request = new QueuedPushRequest("test-scoped-event", group.GroupId, league, year, 1, account.Id, 0, "Test", "Private event", "/live", clock.GetUtcNow().AddHours(2));
        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<AccountApplicationService>();
        await using (var database = factory.Database.CreateContext())
        {
            await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, default);
            var outbox = new PushOutboxApplicationService(database, accounts, clock);
            await outbox.EnqueueAsync(request); await outbox.EnqueueAsync(request);
            await database.SaveChangesAsync(); await transaction.CommitAsync();
        }
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sent = new List<(string Endpoint, string Tag)>();
        var transport = new StubPushTransport(async (device, payload) =>
        {
            sent.Add((device.Endpoint, payload.Tag));
            Assert.Equal(account.Email, payload.Data.RecipientEmail);
            entered.TrySetResult(); await release.Task;
            return device.Endpoint == first.Endpoint ? PushTransportOutcome.Delivered : PushTransportOutcome.Retry;
        });
        await using var dispatcherDatabase = factory.Database.CreateContext();
        var options = Options.Create(new PushTransportOptions { Enabled = true });
        var run = new PushDispatcher(dispatcherDatabase, transport, options, clock).DispatchAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
        await using (var competingDatabase = factory.Database.CreateContext())
        {
            var other = await new PushDispatcher(competingDatabase, transport, options, clock).DispatchAsync();
            Assert.Equal(0, other.Claimed);
        }
        release.TrySetResult();
        Assert.Equal(1, (await run).Retried);
        Assert.Equal(2, sent.Count);
        clock.Advance(TimeSpan.FromMinutes(2));
        transport.Callback = (device, payload) => { sent.Add((device.Endpoint, payload.Tag)); return Task.FromResult(PushTransportOutcome.Delivered); };
        Assert.Equal(1, (await new PushDispatcher(dispatcherDatabase, transport, options, clock).DispatchAsync()).Completed);
        Assert.Equal(3, sent.Count);
        Assert.Equal(1, sent.Count(x => x.Endpoint == first.Endpoint));
        Assert.Single(sent.Select(x => x.Tag).Distinct());
        var stored = await dispatcherDatabase.PushDeliveries.AsNoTracking().SingleAsync(x => x.UserId == account.Id);
        Assert.Equal(PushDeliveryStatus.Completed, stored.Status); Assert.Equal(2, stored.Receipts.Count);
        (await owner.PatchAsJsonAsync("/api/push-notifications", new PushPreferencesPatch(MatchEvents: false))).EnsureSuccessStatusCode();
        await using (var database = factory.Database.CreateContext())
        {
            await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, default);
            await new PushOutboxApplicationService(database, accounts, clock).EnqueueAsync(request with { EventKey = "disabled-event" });
            await database.SaveChangesAsync(); await transaction.CommitAsync();
        }
        Assert.Equal(1, (await new PushDispatcher(dispatcherDatabase, transport, options, clock).DispatchAsync()).Suppressed);
        Assert.Equal(3, sent.Count);
    }

    [PostgreSqlFact]
    public async Task Legacy_push_quarantine_requires_explicit_global_admin_scope_and_preserves_expiry_receipts_and_tag()
    {
        using var owner = await factory.AccountAsync(globalAdmin: true);
        using var outsider = await factory.AccountAsync();
        var account = (await owner.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var group = await CreateGroupAsync(owner);
        var year = FantacalcioTime.ActualYear; var league = await CreateLeagueAsync(owner, group.GroupId, year);
        var graph = (await owner.GetFromJsonAsync<GameGroupDto>($"/api/groups/{group.GroupId}"))!;
        (await owner.PostAsJsonAsync($"/api/workspace/groups/{group.GroupId}/teams", new CreateTeamRequest(graph.Baskets[0].Id, year, "Legacy recipient", account.Id, []))).EnsureSuccessStatusCode();
        var id = Guid.NewGuid(); var deadline = DateTimeOffset.UtcNow.AddMinutes(-5);
        await using (var database = factory.Database.CreateContext())
        {
            database.PushDeliveries.Add(new() { Id = id, DeduplicationKey = id.ToString(), SeasonIndex = year, Day = 0, UserId = account.Id,
                Title = "Legacy", Body = "Do not replay blindly", Status = PushDeliveryStatus.Quarantined, QuarantineReason = "legacy_scope_unverified", Attempts = 4,
                ExpiresAtUtc = deadline, Tag = "original-tag", Receipts = [new() { Ordinal = 0, EndpointHash = "original-hash", Outcome = "legacy_completed" }] });
            await database.SaveChangesAsync();
        }
        var path = $"/api/push-notifications/quarantine/{id}/confirm-scope";
        var confirmation = new ConfirmPushScopeRequest(group.GroupId, league, "/live");
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.PostAsJsonAsync(path, confirmation)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(path, confirmation with { Uri = "https://untrusted.test" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(path, confirmation with { GroupId = Guid.NewGuid() })).StatusCode);
        (await owner.PostAsJsonAsync(path, confirmation)).EnsureSuccessStatusCode();
        await using var final = factory.Database.CreateContext();
        var transport = new StubPushTransport((_, _) => throw new InvalidOperationException("Expired delivery must not send."));
        var result = await new PushDispatcher(final, transport, Options.Create(new PushTransportOptions { Enabled = true }), TimeProvider.System).DispatchAsync();
        Assert.Equal(1, result.Suppressed);
        var saved = await final.PushDeliveries.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(PushDeliveryStatus.Expired, saved.Status); Assert.Equal(5, saved.Attempts);
        Assert.Equal("original-tag", saved.Tag); Assert.Single(saved.Receipts);
        Assert.Equal(1, await final.PushDeliveryScopeConfirmations.CountAsync(x => x.DeliveryId == id));
    }
}

internal sealed class PushTestClock : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan delta) => _now += delta;
}
internal sealed class StubPushTransport(Func<PushSubscriptionDto, PushPayload, Task<PushTransportOutcome>> callback) : IPushDeliveryTransport
{
    public Func<PushSubscriptionDto, PushPayload, Task<PushTransportOutcome>> Callback { get; set; } = callback;
    public Task<PushTransportOutcome> SendAsync(PushSubscriptionDto device, PushPayload payload, int timeToLiveSeconds, CancellationToken cancellationToken) => Callback(device, payload);
}

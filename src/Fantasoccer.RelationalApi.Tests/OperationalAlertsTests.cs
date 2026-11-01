using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Football;
using Fantasoccer.Application.Core.Notifications;
using Fantasoccer.Application.Core.Operations;
using Fantasoccer.Business.IngestionData;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed class OperationalAlertsTests(WorkspaceApiFactory factory) : IClassFixture<WorkspaceApiFactory>
{
    private async Task<(HttpClient Client, AccountIdentityDto Account)> AdminAsync(bool optIn = true)
    {
        // Tests share a fixture, and some advance their fake clock. Do not let
        // a previous test's future failure become a new subscriber's event.
        await using (var clean = factory.Database.CreateContext())
        {
            await clean.PushDeliveries.ExecuteDeleteAsync();
            await clean.SqlJobRuns.ExecuteDeleteAsync();
            await clean.OperationalFailures.ExecuteDeleteAsync();
        }
        var client = await factory.AccountAsync(globalAdmin: true);
        var account = (await client.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        factory.Services.GetRequiredService<IOptions<GlobalAdministratorOptions>>().Value.Emails = [account.Email];
        if (optIn) (await client.PatchAsJsonAsync("/api/push-notifications", new PushPreferencesPatch(OperationalAlerts: true))).EnsureSuccessStatusCode();
        return (client, account);
    }

    [PostgreSqlFact]
    public async Task Operational_preferences_are_opt_in_global_admin_only_and_disable_all_clears_them()
    {
        var (admin, _) = await AdminAsync(optIn: false);
        using var ordinary = await factory.AccountAsync();
        // A super_admin claim (or a group role) alone is not enough.
        ordinary.DefaultRequestHeaders.Add("X-Workspace-Admin", "true");
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.PatchAsJsonAsync("/api/push-notifications", new PushPreferencesPatch(OperationalAlerts: true))).StatusCode);
        var normal = (await ordinary.GetFromJsonAsync<PushSettingsDto>("/api/push-notifications"))!;
        Assert.False(normal.CanReceiveOperationalAlerts); Assert.False(normal.OperationalAlerts);
        var before = (await admin.GetFromJsonAsync<PushSettingsDto>("/api/push-notifications"))!;
        Assert.True(before.CanReceiveOperationalAlerts); Assert.False(before.OperationalAlerts);
        var enabled = await admin.PatchAsJsonAsync("/api/push-notifications", new PushPreferencesPatch(OperationalAlerts: true));
        Assert.True((await enabled.Content.ReadFromJsonAsync<PushSettingsDto>())!.OperationalAlerts);
        (await admin.PostAsync("/api/push-notifications/disable", null)).EnsureSuccessStatusCode();
        Assert.False((await admin.GetFromJsonAsync<PushSettingsDto>("/api/push-notifications"))!.OperationalAlerts);
        admin.Dispose();
    }

    [PostgreSqlFact]
    public async Task Failures_are_deduplicated_and_old_history_normal_waits_and_deploy_cancellation_are_not_alerts()
    {
        var (admin, account) = await AdminAsync(); using var owner = admin;
        var clock = new AlertClock(DateTimeOffset.UtcNow);
        await SeedJobAsync(clock.Now.AddHours(-1), "Failed", "old_before_activation");
        await SeedJobAsync(clock.Now, "Failed", "provider_failed");
        await SeedJobAsync(clock.Now, "Failed", "provider_failed");
        await SeedJobAsync(clock.Now, "Succeeded", null);
        await SeedJobAsync(clock.Now, "Canceled", "job_canceled");
        await SeedJobAsync(clock.Now, "Partial", "results_waiting_for_official_data");
        await SeedJobAsync(clock.Now, "Partial", "postponed_matchdays_pending");
        await SeedJobAsync(clock.Now, "Partial", "hall_batch_pending");
        await SeedImportAsync(clock.Now, "import_catalogue", "football_provider_incomplete");
        Assert.Equal(2, await ProduceAsync(clock));
        Assert.Equal(0, await ProduceAsync(clock));
        clock.Now = clock.Now.AddMinutes(31);
        await SeedJobAsync(clock.Now, "Failed", "provider_failed");
        Assert.Equal(1, await ProduceAsync(clock));
        await using var database = factory.Database.CreateContext();
        var rows = await database.PushDeliveries.Where(x => x.UserId == account.Id).ToArrayAsync();
        Assert.Equal(3, rows.Length);
        Assert.All(rows, row => { Assert.Equal(7, row.Kind); Assert.Null(row.GroupId); Assert.Null(row.LeagueId); Assert.Equal("/logs", row.Uri); });
    }

    [PostgreSqlFact]
    public async Task Operational_dispatch_needs_no_team_and_rechecks_optout_and_revoked_admin_rights()
    {
        var (admin, account) = await AdminAsync(); using var owner = admin;
        using var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var point = key.ExportParameters(false).Q;
        var device = new PushSubscriptionDto("https://fcm.googleapis.com/fcm/send/" + Guid.NewGuid().ToString("N"),
            Convert.ToBase64String(new byte[] { 4 }.Concat(point.X!).Concat(point.Y!).ToArray()), Convert.ToBase64String(new byte[16]));
        (await admin.PostAsJsonAsync("/api/push-notifications/devices", new RegisterNotificationDeviceRequest(device))).EnsureSuccessStatusCode();
        var clock = new AlertClock(DateTimeOffset.UtcNow);
        var transport = new AlertTransport();
        await SeedJobAsync(clock.Now, "Failed", "first_failure");
        Assert.Equal(1, await ProduceAsync(clock));
        async Task<PushDispatchResult> DispatchAsync()
        {
            await using var database = factory.Database.CreateContext();
            await using var scope = factory.Services.CreateAsyncScope();
            return await new PushDispatcher(database, transport, Options.Create(new PushTransportOptions { Enabled = true }), clock,
                scope.ServiceProvider.GetRequiredService<AccountApplicationService>()).DispatchAsync();
        }
        Assert.Equal(1, (await DispatchAsync()).Completed);
        Assert.Equal(account.Email, Assert.Single(transport.Sent).Data.RecipientEmail);
        await SeedJobAsync(clock.Now, "Canceled", "job_timeout");
        Assert.Equal(1, await ProduceAsync(clock));
        (await admin.PatchAsJsonAsync("/api/push-notifications", new PushPreferencesPatch(OperationalAlerts: false))).EnsureSuccessStatusCode();
        Assert.Equal(1, (await DispatchAsync()).Suppressed);
        Assert.Single(transport.Sent);
        (await admin.PatchAsJsonAsync("/api/push-notifications", new PushPreferencesPatch(OperationalAlerts: true))).EnsureSuccessStatusCode();
        clock.Now = DateTimeOffset.UtcNow.AddSeconds(1);
        await SeedJobAsync(clock.Now, "Partial", "league_calculation_failed");
        Assert.Equal(1, await ProduceAsync(clock));
        factory.Services.GetRequiredService<IOptions<GlobalAdministratorOptions>>().Value.Emails = [];
        Assert.Equal(1, (await DispatchAsync()).Suppressed);
        Assert.Single(transport.Sent);
    }

    [PostgreSqlFact]
    public async Task Push_text_never_contains_raw_exception_like_codes_or_urls()
    {
        var (admin, account) = await AdminAsync(); using var owner = admin;
        var clock = new AlertClock(DateTimeOffset.UtcNow);
        await SeedImportAsync(clock.Now, "import_catalogue", "https://provider.invalid/?token=synthetic-secret");
        Assert.Equal(1, await ProduceAsync(clock));
        await using var database = factory.Database.CreateContext();
        var delivery = await database.PushDeliveries.SingleAsync(x => x.UserId == account.Id);
        Assert.Contains("operation_failed", delivery.Body);
        Assert.DoesNotContain("synthetic-secret", delivery.Body); Assert.DoesNotContain("https:", delivery.Body);
    }

    [PostgreSqlFact]
    public async Task Failed_manual_import_is_recorded_separately_and_visible_only_in_admin_logs()
    {
        var (admin, account) = await AdminAsync(); using var owner = admin;
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IAllPlayers>(new EmptyCatalogue())));
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("X-Workspace-User", account.Email); client.DefaultRequestHeaders.Add("X-Workspace-Admin", "true");
        app.Services.GetRequiredService<IOptions<GlobalAdministratorOptions>>().Value.Emails = [account.Email];
        var response = await client.PostAsJsonAsync("/api/football/refresh", new RefreshFootballRequest("catalogue", FantacalcioTime.ActualYear));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var database = factory.Database.CreateContext();
        var failure = await database.OperationalFailures.OrderByDescending(x => x.OccurredAtUtc).FirstAsync();
        Assert.Equal("import_catalogue", failure.Source); Assert.Equal("football_provider_incomplete", failure.Code);
        var logs = (await client.GetFromJsonAsync<OperationalLogDto[]>("/api/operations/logs"))!;
        Assert.Contains(logs, x => x.Id == failure.Id && x.Source == "operational-import");
        using var ordinary = await factory.AccountAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.GetAsync("/api/operations/logs")).StatusCode);
        var count = await database.OperationalFailures.CountAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.PostAsJsonAsync("/api/football/refresh", new RefreshFootballRequest("catalogue", FantacalcioTime.ActualYear))).StatusCode);
        Assert.Equal(count, await database.OperationalFailures.CountAsync());
    }

    [PostgreSqlFact]
    public async Task Producer_never_targets_a_non_admin_even_if_a_stale_preference_is_enabled()
    {
        var (admin, _) = await AdminAsync(optIn: false); using var owner = admin;
        using var ordinary = await factory.AccountAsync();
        var account = (await ordinary.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        await using (var database = factory.Database.CreateContext())
        {
            database.UserPushPreferences.Add(new() { UserId = account.Id, OperationalAlerts = true, OperationalAlertsEnabledAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1) });
            await database.SaveChangesAsync();
        }
        var clock = new AlertClock(DateTimeOffset.UtcNow);
        await SeedJobAsync(clock.Now, "Failed", "provider_failed");
        Assert.Equal(0, await ProduceAsync(clock));
        await using var final = factory.Database.CreateContext();
        Assert.Empty(await final.PushDeliveries.ToArrayAsync());
    }

    [PostgreSqlFact]
    public async Task Operational_failure_receipts_have_the_same_thirty_day_retention_as_job_runs()
    {
        var (admin, _) = await AdminAsync(); using var owner = admin;
        await SeedImportAsync(DateTimeOffset.UtcNow.AddDays(-31), "import_live", "old_failure");
        await SeedImportAsync(DateTimeOffset.UtcNow.AddDays(-29), "import_live", "recent_failure");
        await using var scope = factory.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISqlJobActions>().ExecuteAsync("job-retention", FantacalcioTime.ActualYear, default);
        Assert.Equal(1, result.Processed);
        await using var database = factory.Database.CreateContext();
        Assert.Equal("recent_failure", Assert.Single(await database.OperationalFailures.ToArrayAsync()).Code);
    }

    private async Task SeedJobAsync(DateTimeOffset at, string status, string? code)
    {
        var year = FantacalcioTime.ActualYear;
        await using var database = factory.Database.CreateContext();
        if (!await database.Seasons.AnyAsync(x => x.SeasonIndex == year)) database.Seasons.Add(new() { SeasonIndex = year, StartYear = year + 2011 });
        if (!await database.SqlJobStates.AnyAsync(x => x.Name == "images" && x.SeasonIndex == year)) database.SqlJobStates.Add(new() { Name = "images", SeasonIndex = year });
        database.SqlJobRuns.Add(new() { Id = Guid.NewGuid(), Name = "images", SeasonIndex = year, StartedAtUtc = at.AddSeconds(-1), CompletedAtUtc = at, Status = status, Code = code });
        await database.SaveChangesAsync();
    }

    private async Task SeedImportAsync(DateTimeOffset at, string source, string code)
    {
        await using var database = factory.Database.CreateContext();
        database.OperationalFailures.Add(new() { Id = Guid.NewGuid(), Source = source, Code = code, OccurredAtUtc = at });
        await database.SaveChangesAsync();
    }

    private async Task<int> ProduceAsync(TimeProvider clock)
    {
        await using var database = factory.Database.CreateContext();
        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<AccountApplicationService>();
        return await new OperationalAlertProducer(database, accounts, new PushOutboxApplicationService(database, accounts, clock), clock).RunAsync(FantacalcioTime.ActualYear);
    }

    private sealed class AlertClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class AlertTransport : IPushDeliveryTransport
    {
        public List<PushPayload> Sent { get; } = [];
        public Task<PushTransportOutcome> SendAsync(PushSubscriptionDto device, PushPayload payload, int ttl, CancellationToken token)
        { Sent.Add(payload); return Task.FromResult(PushTransportOutcome.ProviderAccepted); }
    }
    private sealed class EmptyCatalogue : IAllPlayers
    {
        public Task<List<RealPlayer>> GetAsync() => Task.FromResult(new List<RealPlayer>());
    }
}

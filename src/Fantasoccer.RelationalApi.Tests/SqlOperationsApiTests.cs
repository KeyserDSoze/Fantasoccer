using System.Net;
using System.Net.Http.Json;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Operations;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using System.Security.Claims;
using Fantasoccer.Application.Api.Operations;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed partial class WorkspaceApiTests
{
    [PostgreSqlFact]
    public async Task SQL_job_fencing_token_refuses_application_writes_after_another_runner_replaces_the_lease()
    {
        using var admin = await factory.AccountAsync(globalAdmin: true); var email = admin.DefaultRequestHeaders.GetValues("X-Workspace-User").Single();
        var year = FantacalcioTime.ActualYear;
        var actions = new StubSqlJobs(async token =>
        {
            await using (var changed = factory.Database.CreateContext())
            {
                var state = await changed.SqlJobStates.SingleAsync(x => x.Name == "statistics" && x.SeasonIndex == year, token);
                state.LeaseId = Guid.NewGuid(); state.LeaseUntilUtc = DateTimeOffset.UtcNow.AddMinutes(5);
                await changed.SaveChangesAsync(token);
            }
            await using var stale = factory.Database.CreateContext();
            await Assert.ThrowsAsync<InvalidOperationException>(() => CoreMigrationLock.BeginInteractiveWriteAsync(stale, token));
            return new(0, 1, "lease_fenced");
        });
        await using var scope = factory.Services.CreateAsyncScope(); await using var database = factory.Database.CreateContext();
        var caller = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.Email, email), new(ClaimTypes.Role, "super_admin")], "test"));
        var service = new SqlJobApplicationService(database, scope.ServiceProvider.GetRequiredService<OperationsApplicationService>(), actions, TimeProvider.System);
        var result = (await service.RunAsync(caller, new("statistics", year)))!;
        Assert.Equal("LeaseLost", result.Status);
        await using var cleanup = factory.Database.CreateContext();
        var state = await cleanup.SqlJobStates.SingleAsync(x => x.Name == "statistics" && x.SeasonIndex == year);
        state.LeaseId = null; state.LeaseUntilUtc = null;
        var previous = await cleanup.SqlJobRuns.SingleAsync(x => x.Id == result.RunId); previous.Status = "LeaseLost";
        await cleanup.SaveChangesAsync();
    }

    [PostgreSqlFact]
    public async Task Market_expiration_job_emits_one_system_event_and_never_reexecutes_or_expires_a_completed_trade()
    {
        using var owner = await factory.AccountAsync(globalAdmin: true); using var seller = await factory.AccountAsync();
        var first = (await owner.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!; var second = (await seller.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var group = await CreateGroupAsync(owner); var year = FantacalcioTime.ActualYear; var league = await CreateLeagueAsync(owner, group.GroupId, year);
        (await owner.PutAsJsonAsync($"/api/workspace/groups/{group.GroupId}/members", new SetGroupMemberRequest(second.Email, "Seller", 2))).EnsureSuccessStatusCode();
        var graph = (await owner.GetFromJsonAsync<GameGroupDto>($"/api/groups/{group.GroupId}"))!; var basket = graph.Baskets[0].Id; var teams = new List<Guid>();
        foreach (var account in new[] { first, second })
        {
            var created = await owner.PostAsJsonAsync($"/api/workspace/groups/{group.GroupId}/teams", new CreateTeamRequest(basket, year, account.Id == first.Id ? "Expiry buyer" : "Expiry seller", account.Id, []));
            created.EnsureSuccessStatusCode(); teams.Add((await created.Content.ReadFromJsonAsync<TeamCreatedDto>())!.TeamId);
        }
        var pending = Guid.NewGuid(); var completed = Guid.NewGuid();
        await using (var database = factory.Database.CreateContext())
        {
            database.MarketBooks.Add(new() { GroupId = group.GroupId, LeagueId = league, SeasonIndex = year });
            foreach (var (id, status) in new[] { (pending, 0), (completed, 1) })
                database.MarketTrades.Add(new() { Id = id, GroupId = group.GroupId, LeagueId = league, SeasonIndex = year, Status = status,
                    BuyerTeamId = teams[0], BuyerBasketId = basket, SellerTeamId = teams[1], SellerBasketId = basket, BuyerOwner = first.Email, SellerOwner = second.Email,
                    CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-15) });
            await database.SaveChangesAsync();
        }
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISqlJobActions>().ExecuteAsync("market-expiry", year, default);
            Assert.Equal(0, (await scope.ServiceProvider.GetRequiredService<ISqlJobActions>().ExecuteAsync("market-expiry", year, default)).Processed);
        }
        await using var final = factory.Database.CreateContext();
        Assert.Equal(6, (await final.MarketTrades.SingleAsync(x => x.Id == pending)).Status);
        Assert.Equal(1, (await final.MarketTrades.SingleAsync(x => x.Id == completed)).Status);
        var audit = Assert.Single(await final.MarketTradeEvents.Where(x => x.TradeId == pending).ToArrayAsync());
        Assert.Equal("expired", audit.Kind); Assert.Null(audit.ActorUserId); Assert.Equal(2, audit.TradeVersion);
        Assert.All(await final.RosterSnapshots.Where(x => x.GroupId == group.GroupId).ToArrayAsync(), x => Assert.Equal(0, x.MarketBalanceAdjustment));
    }

    [PostgreSqlFact]
    public async Task SQL_scheduler_can_start_without_legacy_writers_and_expired_leases_are_recovered_without_overwriting_old_runs()
    {
        using var admin = await factory.AccountAsync(globalAdmin: true); var email = admin.DefaultRequestHeaders.GetValues("X-Workspace-User").Single();
        var actions = new StubSqlJobs(_ => Task.FromResult(new SqlJobWorkResult(1, 0)));
        using (var scheduled = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("BackgroundJobs:Enabled", "true"); builder.UseSetting("ExternalHttp:Enabled", "false");
            builder.ConfigureServices(services => services.AddSingleton<ISqlJobActions>(actions));
        }))
        {
            using var health = scheduled.CreateClient(); (await health.GetAsync("/health/ready")).EnsureSuccessStatusCode();
            Assert.Contains(scheduled.Services.GetServices<IHostedService>(), service => service is SqlJobsWorker);
        }
        var previous = Guid.NewGuid(); var year = FantacalcioTime.ActualYear;
        await using (var database = factory.Database.CreateContext())
        {
            var state = await database.SqlJobStates.SingleOrDefaultAsync(x => x.Name == "images" && x.SeasonIndex == year);
            if (state == null) database.SqlJobStates.Add(state = new() { Name = "images", SeasonIndex = year });
            state.LeaseId = previous; state.LeaseUntilUtc = DateTimeOffset.UtcNow.AddMinutes(-1); state.LastStatus = "Running";
            database.SqlJobRuns.Add(new() { Id = previous, Name = "images", SeasonIndex = year, StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-6), Status = "Running" });
            await database.SaveChangesAsync();
        }
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<ISqlJobActions>(actions)));
        app.Services.GetRequiredService<IOptions<GlobalAdministratorOptions>>().Value.Emails = [email];
        using var client = app.CreateClient(); client.DefaultRequestHeaders.Add("X-Workspace-User", email); client.DefaultRequestHeaders.Add("X-Workspace-Admin", "true");
        (await client.PostAsJsonAsync("/api/operations/jobs/run", new RunSqlJobRequest("images", year))).EnsureSuccessStatusCode();
        await using var final = factory.Database.CreateContext();
        Assert.Equal("LeaseLost", (await final.SqlJobRuns.SingleAsync(x => x.Id == previous)).Status);
        Assert.Null((await final.SqlJobStates.SingleAsync(x => x.Name == "images" && x.SeasonIndex == year)).LeaseId);
    }

    [PostgreSqlFact]
    public async Task Cancelled_SQL_job_releases_its_lease_and_records_a_receipt_after_the_request_token_is_cancelled()
    {
        using var admin = await factory.AccountAsync(globalAdmin: true); var email = admin.DefaultRequestHeaders.GetValues("X-Workspace-User").Single();
        using var cancellation = new CancellationTokenSource();
        var actions = new StubSqlJobs(token => { cancellation.Cancel(); return Task.FromCanceled<SqlJobWorkResult>(token); });
        await using var scope = factory.Services.CreateAsyncScope(); await using var database = factory.Database.CreateContext();
        var caller = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.Email, email), new(ClaimTypes.Role, "super_admin")], "test"));
        var service = new SqlJobApplicationService(database, scope.ServiceProvider.GetRequiredService<OperationsApplicationService>(), actions, TimeProvider.System);
        var result = (await service.RunAsync(caller, new("chances", FantacalcioTime.ActualYear), cancellation.Token))!;
        Assert.Equal("Canceled", result.Status); Assert.Equal("job_canceled", result.Code);
        await using var final = factory.Database.CreateContext();
        Assert.Equal("Canceled", (await final.SqlJobRuns.SingleAsync(x => x.Id == result.RunId)).Status);
        Assert.Null((await final.SqlJobStates.SingleAsync(x => x.Name == "chances" && x.SeasonIndex == FantacalcioTime.ActualYear)).LeaseId);
    }

    [PostgreSqlFact]
    public async Task SQL_job_leases_reject_concurrent_runs_and_global_logs_are_bounded_authorized_and_redacted()
    {
        using var admin = await factory.AccountAsync(globalAdmin: true); using var ordinary = await factory.AccountAsync();
        var email = admin.DefaultRequestHeaders.GetValues("X-Workspace-User").Single();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var actions = new StubSqlJobs(async token => { entered.TrySetResult(); await release.Task.WaitAsync(token); return new(3, 0); });
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<ISqlJobActions>(actions)));
        using var client = app.CreateClient(); client.DefaultRequestHeaders.Add("X-Workspace-User", email); client.DefaultRequestHeaders.Add("X-Workspace-Admin", "true");
        app.Services.GetRequiredService<IOptions<GlobalAdministratorOptions>>().Value.Emails = [email];
        var request = new RunSqlJobRequest("calendar", FantacalcioTime.ActualYear);
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.PostAsJsonAsync("/api/operations/jobs/run", request)).StatusCode);
        var first = client.PostAsJsonAsync("/api/operations/jobs/run", request);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/operations/jobs/run", request)).StatusCode);
        release.TrySetResult(); var response = await first; response.EnsureSuccessStatusCode();
        var result = (await response.Content.ReadFromJsonAsync<SqlJobResult>())!; Assert.Equal("Succeeded", result.Status); Assert.Equal(3, result.Processed);
        actions.Callback = _ => throw new HttpRequestException("https://private.test/path?token=synthetic-secret");
        var failure = await client.PostAsJsonAsync("/api/operations/jobs/run", request);
        failure.EnsureSuccessStatusCode(); Assert.Equal("Failed", (await failure.Content.ReadFromJsonAsync<SqlJobResult>())!.Status);
        var logId = Guid.NewGuid();
        await using (var database = factory.Database.CreateContext())
        {
            database.ArchivedOperationalLogs.Add(new() { Id = logId, PartitionKey = "AError", RowKey = logId.ToString(), TimestampUtc = DateTimeOffset.UtcNow, ETag = "synthetic",
                Message = "Source at https://private.test/path?sig=synthetic-secret", Error = "access_token=synthetic-secret" });
            await database.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.GetAsync("/api/operations/logs")).StatusCode);
        var log = (await client.GetFromJsonAsync<OperationalLogDto>($"/api/operations/logs/{logId}"))!;
        Assert.DoesNotContain("synthetic-secret", log.Message + log.Error); Assert.Contains("redacted", log.Message + log.Error);
        Assert.Single((await client.GetFromJsonAsync<OperationalLogDto[]>("/api/operations/logs?limit=1"))!);
        Assert.True((await client.GetFromJsonAsync<OperationalLogStatistics>("/api/operations/logs/statistics"))!.Total >= 3);
        await using var final = factory.Database.CreateContext();
        Assert.DoesNotContain(await final.SqlJobRuns.Where(x => x.Id == result.RunId).ToArrayAsync(), x => (x.Code ?? "").Contains("secret"));
    }
}
internal sealed class StubSqlJobs(Func<CancellationToken, Task<SqlJobWorkResult>> callback) : ISqlJobActions
{
    public Func<CancellationToken, Task<SqlJobWorkResult>> Callback { get; set; } = callback;
    public Task<SqlJobWorkResult> ExecuteAsync(string name, int year, CancellationToken cancellationToken) => Callback(cancellationToken);
}
public sealed class SqlJobScheduleTests
{
    [Fact]
    public void Rome_schedule_is_strictly_future_and_handles_fall_back_and_semiannual_images()
    {
        var now = new DateTimeOffset(2026, 10, 25, 0, 5, 0, TimeSpan.Zero);
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 1, 0, 0, TimeSpan.Zero), SqlJobSchedule.Next("market-expiry", now));
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 0, 6, 0, TimeSpan.Zero), SqlJobSchedule.Next("live", now));
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 0, 10, 0, TimeSpan.Zero), SqlJobSchedule.Next("postponed-results", now));
        Assert.Contains("postponed-results", SqlJobSchedule.Names);
        var images = TimeZoneInfo.ConvertTime(SqlJobSchedule.Next("images", now), TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome"));
        Assert.Equal(3, images.Month); Assert.Equal(1, images.Day); Assert.Equal(3, images.Hour);
        Assert.Throws<ArgumentException>(() => SqlJobSchedule.Next("unknown", now));
    }
}

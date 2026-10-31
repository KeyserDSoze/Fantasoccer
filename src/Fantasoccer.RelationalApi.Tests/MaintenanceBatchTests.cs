using System.Net.Http.Json;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Application.Core.Operations;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed class MaintenanceBatchTests(WorkspaceApiFactory factory) : IClassFixture<WorkspaceApiFactory>
{
    [PostgreSqlFact]
    public async Task Hall_batches_persist_progress_past_a_blocked_league_resume_soon_and_wrap_after_the_full_sweep()
    {
        var year = FantacalcioTime.ActualYear;
        using var owner = await factory.AccountAsync(globalAdmin: true);
        var account = (await owner.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var created = await owner.PostAsJsonAsync("/api/workspace/groups", new CreateGroupRequest("Hall batch test"));
        created.EnsureSuccessStatusCode(); var group = (await created.Content.ReadFromJsonAsync<GroupSummaryDto>())!.GroupId;
        var basketResponse = await owner.PostAsJsonAsync($"/api/workspace/groups/{group}/baskets", new CreateBasketRequest("Batch basket", year));
        basketResponse.EnsureSuccessStatusCode(); var basket = (await basketResponse.Content.ReadFromJsonAsync<BasketCreatedDto>())!.BasketId;
        for (var index = 0; index < 3; index++)
            (await owner.PostAsJsonAsync($"/api/workspace/groups/{group}/leagues", new CreateLeagueRequest("Batch league " + index, year, [basket]))).EnsureSuccessStatusCode();
        Guid[] ordered;
        await using (var database = factory.Database.CreateContext())
        {
            ordered = await database.Leagues.Where(x => x.GroupId == group).OrderBy(x => x.LeagueId).Select(x => x.LeagueId).ToArrayAsync();
            database.HallArchives.Add(new() { GroupId = group, LeagueId = ordered[0], ContributionsInitialized = false });
            await database.SaveChangesAsync();
        }
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.Configure<JobMaintenanceOptions>(x => x.HallBatchSize = 2)));
        app.Services.GetRequiredService<IOptions<GlobalAdministratorOptions>>().Value.Emails = [account.Email];
        using var client = app.CreateClient(); client.DefaultRequestHeaders.Add("X-Workspace-User", account.Email); client.DefaultRequestHeaders.Add("X-Workspace-Admin", "true");
        async Task<SqlJobResult> Run()
        {
            var response = await client.PostAsJsonAsync("/api/operations/jobs/run", new RunSqlJobRequest("hall", year));
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<SqlJobResult>())!;
        }
        var first = await Run();
        Assert.Equal("Partial", first.Status); Assert.Equal(1, first.Processed); Assert.Equal(1, first.Skipped); Assert.Equal("hall_batch_pending", first.Code);
        await using (var database = factory.Database.CreateContext())
        {
            var state = await database.SqlJobStates.SingleAsync(x => x.Name == "hall" && x.SeasonIndex == year);
            Assert.Equal(ordered[1], state.CursorLeagueId);
            Assert.InRange((state.NextRunAtUtc - DateTimeOffset.UtcNow).TotalSeconds, 30, 65);
            Assert.False(await database.HallArchives.AnyAsync(x => x.LeagueId == ordered[2]));
        }
        var second = await Run();
        Assert.Equal("Succeeded", second.Status); Assert.Equal(1, second.Processed);
        await using (var database = factory.Database.CreateContext())
        {
            Assert.True(await database.HallArchives.AnyAsync(x => x.LeagueId == ordered[2]));
            var state = await database.SqlJobStates.SingleAsync(x => x.Name == "hall" && x.SeasonIndex == year);
            Assert.Null(state.CursorGroupId); Assert.Null(state.CursorLeagueId);
        }
        Assert.Equal("Partial", (await Run()).Status); // wraps and revisits the blocked first league, without starving later ones
    }

    [PostgreSqlFact]
    public async Task Job_failure_logs_include_the_exception_type_and_run_ID_but_never_the_exception_message()
    {
        using var owner = await factory.AccountAsync(globalAdmin: true);
        var account = (await owner.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var logger = new RecordingLogger();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddSingleton<ILogger<SqlJobApplicationService>>(logger);
            services.AddSingleton<ISqlJobActions>(new StubSqlJobs(_ => throw new HttpRequestException("https://private.example/path?sig=synthetic-secret")));
        }));
        app.Services.GetRequiredService<IOptions<GlobalAdministratorOptions>>().Value.Emails = [account.Email];
        using var client = app.CreateClient(); client.DefaultRequestHeaders.Add("X-Workspace-User", account.Email); client.DefaultRequestHeaders.Add("X-Workspace-Admin", "true");
        var response = await client.PostAsJsonAsync("/api/operations/jobs/run", new RunSqlJobRequest("calendar", FantacalcioTime.ActualYear));
        response.EnsureSuccessStatusCode(); var result = (await response.Content.ReadFromJsonAsync<SqlJobResult>())!;
        Assert.Equal("Failed", result.Status);
        var message = Assert.Single(logger.Messages);
        Assert.Contains("HttpRequestException", message); Assert.Contains(result.RunId.ToString(), message);
        Assert.DoesNotContain("synthetic-secret", message); Assert.DoesNotContain("private.example", message);
    }

    private sealed class RecordingLogger : ILogger<SqlJobApplicationService>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}

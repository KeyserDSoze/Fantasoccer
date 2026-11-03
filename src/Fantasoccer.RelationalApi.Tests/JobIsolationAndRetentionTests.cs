using Fantasoccer.Application.Api.Operations;
using Fantasoccer.Application.Core.Operations;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed class JobIsolationAndRetentionTests(WorkspaceApiFactory factory) : IClassFixture<WorkspaceApiFactory>
{
    [PostgreSqlFact]
    public async Task An_application_write_holds_its_lease_row_until_commit_even_with_independent_lease_locks()
    {
        using var admin = await factory.AccountAsync(globalAdmin: true);
        var year = FantacalcioTime.ActualYear; var lease = Guid.NewGuid();
        await using (var database = factory.Database.CreateContext())
        {
            if (!await database.Seasons.AnyAsync(x => x.SeasonIndex == year)) database.Seasons.Add(new() { SeasonIndex = year, StartYear = year + 2011 });
            var state = await database.SqlJobStates.SingleOrDefaultAsync(x => x.Name == "chances" && x.SeasonIndex == year);
            if (state is null) database.SqlJobStates.Add(state = new() { Name = "chances", SeasonIndex = year });
            state.LeaseId = lease; state.LeaseUntilUtc = DateTimeOffset.UtcNow.AddMinutes(5);
            await database.SaveChangesAsync();
        }
        using var fence = SqlJobLeaseFence.Enter("chances", year, lease, () => DateTimeOffset.UtcNow);
        await using var writing = factory.Database.CreateContext();
        await using var write = await CoreMigrationLock.BeginInteractiveWriteAsync(writing, default);
        await using (var replacement = factory.Database.CreateContext())
        {
            await using var attempt = await replacement.Database.BeginTransactionAsync();
            await replacement.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '100ms'");
            var error = await Assert.ThrowsAsync<PostgresException>(() => replacement.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"SqlJobStates\" SET \"LeaseId\" = {Guid.NewGuid()} WHERE \"Name\" = 'chances' AND \"SeasonIndex\" = {year}"));
            Assert.Equal("55P03", error.SqlState);
            await attempt.RollbackAsync();
        }
        await write.CommitAsync();
        await using var after = factory.Database.CreateContext();
        Assert.Equal(1, await after.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"SqlJobStates\" SET \"LeaseId\" = {Guid.NewGuid()} WHERE \"Name\" = 'chances' AND \"SeasonIndex\" = {year}"));
    }

    [PostgreSqlFact]
    public async Task Retention_keeps_thirty_days_running_runs_and_receipts_referenced_by_a_lease()
    {
        using var admin = await factory.AccountAsync(globalAdmin: true);
        var year = FantacalcioTime.ActualYear;
        var now = DateTimeOffset.UtcNow;
        var old = Guid.NewGuid(); var recent = Guid.NewGuid(); var running = Guid.NewGuid(); var referenced = Guid.NewGuid();
        await using (var database = factory.Database.CreateContext())
        {
            if (!await database.Seasons.AnyAsync(x => x.SeasonIndex == year)) database.Seasons.Add(new() { SeasonIndex = year, StartYear = year + 2011 });
            foreach (var (id, days, status) in new[] { (old, 31, "Succeeded"), (recent, 29, "Failed"), (running, 60, "Running"), (referenced, 60, "Canceled") })
                database.SqlJobRuns.Add(new() { Id = id, Name = "images", SeasonIndex = year, StartedAtUtc = now.AddDays(-days),
                    CompletedAtUtc = status == "Running" ? null : now.AddDays(-days), Status = status });
            var state = await database.SqlJobStates.SingleOrDefaultAsync(x => x.Name == "images" && x.SeasonIndex == year);
            if (state is null) database.SqlJobStates.Add(state = new() { Name = "images", SeasonIndex = year });
            state.LeaseId = referenced; state.LeaseUntilUtc = now.AddMinutes(5);
            await database.SaveChangesAsync();
        }
        await using var scope = factory.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISqlJobActions>().ExecuteAsync("job-retention", year, default);
        Assert.Equal(1, result.Processed);
        await using var final = factory.Database.CreateContext();
        Assert.False(await final.SqlJobRuns.AnyAsync(x => x.Id == old));
        Assert.Equal(3, await final.SqlJobRuns.CountAsync(x => x.Id == recent || x.Id == running || x.Id == referenced));
        Assert.Equal(referenced, (await final.SqlJobStates.SingleAsync(x => x.Name == "images" && x.SeasonIndex == year)).LeaseId);
    }

    [PostgreSqlFact]
    public async Task Lease_bookkeeping_for_one_job_is_not_blocked_by_an_unrelated_business_write()
    {
        using var admin = await factory.AccountAsync(globalAdmin: true);
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<ISqlJobActions>(new NoWork())));
        using var client = app.CreateClient();
        await using (var database = factory.Database.CreateContext())
        {
            await database.SqlJobStates.Where(x => x.Name == "catalogue").ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.NextRunAtUtc, DateTimeOffset.UtcNow.AddMinutes(-1)).SetProperty(x => x.LeaseId, (Guid?)null).SetProperty(x => x.LeaseUntilUtc, (DateTimeOffset?)null));
            // Production has already crossed the first-interactive-write boundary.
            // The test account factory itself intentionally bypasses that boundary.
            await using var initialized = await CoreMigrationLock.BeginInteractiveWriteAsync(database, default);
            await database.SaveChangesAsync();
            await initialized.CommitAsync();
        }
        await using var held = factory.Database.CreateContext();
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(held, default);
        await using var scope = app.Services.CreateAsyncScope();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await scope.ServiceProvider.GetRequiredService<SqlJobApplicationService>().RunScheduledAsync("catalogue", FantacalcioTime.ActualYear, deadline.Token);
        Assert.NotNull(result); Assert.Equal("Succeeded", result.Status);
        await transaction.RollbackAsync();
    }

    [PostgreSqlFact]
    public async Task A_job_whose_schedule_became_more_frequent_runs_at_once_instead_of_waiting_for_the_old_slot()
    {
        using var admin = await factory.AccountAsync(globalAdmin: true);
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<ISqlJobActions>(new NoWork())));
        using var client = app.CreateClient();
        var year = FantacalcioTime.ActualYear;
        await using (var scope = app.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<SqlJobApplicationService>().RunScheduledAsync("official", year);
        // As after a deploy: the last run stored the next slot of the old schedule, hours away; the current schedule has passed slots since.
        await using (var database = factory.Database.CreateContext())
            await database.SqlJobStates.Where(x => x.Name == "official" && x.SeasonIndex == year).ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.NextRunAtUtc, DateTimeOffset.UtcNow.AddHours(3)).SetProperty(x => x.LastCompletedAtUtc, (DateTimeOffset?)DateTimeOffset.UtcNow.AddMinutes(-40))
                .SetProperty(x => x.LeaseId, (Guid?)null).SetProperty(x => x.LeaseUntilUtc, (DateTimeOffset?)null));
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<SqlJobApplicationService>().RunScheduledAsync("official", year);
            Assert.NotNull(result); Assert.Equal("Succeeded", result.Status);
        }
        // Once it has run, the next time follows the current schedule.
        await using (var database = factory.Database.CreateContext())
        {
            var state = await database.SqlJobStates.SingleAsync(x => x.Name == "official" && x.SeasonIndex == year);
            Assert.InRange((state.NextRunAtUtc - DateTimeOffset.UtcNow).TotalMinutes, 0, 15.1);
        }
        await using (var scope = app.Services.CreateAsyncScope())
            Assert.Null(await scope.ServiceProvider.GetRequiredService<SqlJobApplicationService>().RunScheduledAsync("official", year));
    }

    [PostgreSqlFact]
    public async Task A_slow_calendar_provider_does_not_prevent_the_catalogue_job_from_starting()
    {
        using var admin = await factory.AccountAsync(globalAdmin: true);
        var jobs = new SlowProvider();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<ISqlJobActions>(jobs)));
        using var client = app.CreateClient();
        // Due now even if another test already initialized these scheduler states.
        await using (var database = factory.Database.CreateContext())
            await database.SqlJobStates.Where(x => x.Name == "calendar" || x.Name == "catalogue").ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.NextRunAtUtc, DateTimeOffset.UtcNow.AddMinutes(-1)).SetProperty(x => x.LeaseId, (Guid?)null).SetProperty(x => x.LeaseUntilUtc, (DateTimeOffset?)null));
        using var worker = new SqlJobsWorker(app.Services.GetRequiredService<IServiceScopeFactory>(), app.Services.GetRequiredService<ILogger<SqlJobsWorker>>(),
            Options.Create(new JobMaintenanceOptions { MaxConcurrentJobs = 2, StartDelaySeconds = 0 }));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await worker.StartAsync(default);
            await jobs.CalendarStarted.Task.WaitAsync(deadline.Token);
            await jobs.CatalogueStarted.Task.WaitAsync(deadline.Token);
            Assert.False(jobs.Release.Task.IsCompleted);
        }
        finally
        {
            jobs.Release.TrySetResult();
            await worker.StopAsync(deadline.Token);
        }
    }

    private sealed class NoWork : ISqlJobActions
    {
        public Task<SqlJobWorkResult> ExecuteAsync(string name, int year, CancellationToken token) => Task.FromResult(new SqlJobWorkResult(0, 0));
    }

    private sealed class SlowProvider : ISqlJobActions
    {
        public TaskCompletionSource CalendarStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CatalogueStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<SqlJobWorkResult> ExecuteAsync(string name, int year, CancellationToken token)
        {
            if (name == "calendar") { CalendarStarted.TrySetResult(); await Release.Task.WaitAsync(token); }
            if (name == "catalogue") CatalogueStarted.TrySetResult();
            return new(0, 0);
        }
    }
}

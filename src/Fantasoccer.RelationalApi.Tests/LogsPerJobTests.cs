using System.Net.Http.Json;
using Fantasoccer.Application.Core.Operations;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed class LogsPerJobTests(WorkspaceApiFactory factory) : IClassFixture<WorkspaceApiFactory>
{
    [PostgreSqlFact]
    public async Task Every_job_keeps_its_own_share_of_the_log_so_a_busy_job_does_not_push_out_the_others_and_failures_stay_in_view()
    {
        using var admin = await factory.AccountAsync(globalAdmin: true);
        var year = FantacalcioTime.ActualYear; var now = DateTimeOffset.UtcNow;
        await using (var database = factory.Database.CreateContext())
        {
            if (!await database.Seasons.AnyAsync(x => x.SeasonIndex == year)) database.Seasons.Add(new() { SeasonIndex = year, StartYear = year + 2011 });
            foreach (var name in new[] { "formations", "chances", "catalogue" })
            {
                await database.SqlJobRuns.Where(x => x.Name == name).ExecuteDeleteAsync();
                if (!await database.SqlJobStates.AnyAsync(x => x.Name == name && x.SeasonIndex == year)) database.SqlJobStates.Add(new() { Name = name, SeasonIndex = year });
            }
            await database.SaveChangesAsync();
            // A job every minute (150 runs, one failure long ago), another a few times a day, one a day.
            for (var i = 0; i < 150; i++) database.SqlJobRuns.Add(new() { Id = Guid.NewGuid(), Name = "formations", SeasonIndex = year, StartedAtUtc = now.AddMinutes(-i - 1), CompletedAtUtc = now.AddMinutes(-i - 1),
                Status = i == 120 ? "Failed" : "Succeeded", Code = i == 120 ? "job_execution_failed" : null });
            for (var i = 0; i < 40; i++) database.SqlJobRuns.Add(new() { Id = Guid.NewGuid(), Name = "chances", SeasonIndex = year, StartedAtUtc = now.AddHours(-i - 1), CompletedAtUtc = now.AddHours(-i - 1), Status = "Succeeded" });
            for (var i = 0; i < 15; i++) database.SqlJobRuns.Add(new() { Id = Guid.NewGuid(), Name = "catalogue", SeasonIndex = year, StartedAtUtc = now.AddDays(-i - 1), CompletedAtUtc = now.AddDays(-i - 1), Status = "Succeeded" });
            await database.SaveChangesAsync();
        }
        var logs = (await admin.GetFromJsonAsync<OperationalLogDto[]>("/api/operations/logs?limit=100"))!.Where(x => x.Source == "sql-job").ToArray();
        int Count(string job) => logs.Count(x => x.SecondaryKey.StartsWith(job + "_"));
        Assert.Equal(20 + 1, Count("formations")); // the latest twenty and, apart, the failure that is 120 runs old
        Assert.Equal(30, Count("chances"));
        Assert.Equal(10, Count("catalogue"));
        Assert.Single(logs, x => x.SecondaryKey.StartsWith("formations_") && x.Error == "job_execution_failed");
        // Only failures: its own top too.
        var errors = (await admin.GetFromJsonAsync<OperationalLogDto[]>("/api/operations/logs?limit=100&errorsOnly=true"))!.Where(x => x.Source == "sql-job").ToArray();
        Assert.Single(errors, x => x.SecondaryKey.StartsWith("formations_"));
        Assert.Equal(0, SqlJobLogPolicy.FailureSourceLimit - 20);
        Assert.Equal(10, SqlJobLogPolicy.Limit("images"));
    }
}

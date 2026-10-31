using System.Security.Claims;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fantasoccer.Application.Core.Operations;

public sealed record SqlJobWorkResult(int Processed, int Skipped, string? Code = null)
{
    public bool HasMore { get; init; }
}
public interface ISqlJobActions { Task<SqlJobWorkResult> ExecuteAsync(string name, int year, CancellationToken cancellationToken); }
public sealed record RunSqlJobRequest(string Name, int Year);
public sealed record SqlJobResult(Guid RunId, string Name, int Year, string Status, int Processed, int Skipped, string? Code);
public sealed record SqlJobStateDto(string Name, int Year, DateTimeOffset NextRunAtUtc, DateTimeOffset? LeaseUntilUtc, string? LastStatus);

public sealed class SqlJobApplicationService(FantasoccerDbContext database, OperationsApplicationService operations, ISqlJobActions actions, TimeProvider clock,
    ILogger<SqlJobApplicationService> logger)
{
    public async Task<SqlJobStateDto[]> StatesAsync(ClaimsPrincipal caller, CancellationToken cancellationToken = default)
    {
        await operations.AdministratorAsync(caller, cancellationToken);
        return await database.SqlJobStates.AsNoTracking().OrderBy(x => x.Name).Select(x => new SqlJobStateDto(x.Name, x.SeasonIndex, x.NextRunAtUtc, x.LeaseUntilUtc, x.LastStatus)).ToArrayAsync(cancellationToken);
    }
    public async Task<SqlJobResult?> RunAsync(ClaimsPrincipal caller, RunSqlJobRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await operations.AdministratorAsync(caller, cancellationToken);
        return await RunCoreAsync(request.Name, request.Year, actor.Id, true, cancellationToken, caller);
    }
    public Task<SqlJobResult?> RunScheduledAsync(string name, int year, CancellationToken cancellationToken = default) => RunCoreAsync(name, year, null, false, cancellationToken);

    private async Task<SqlJobResult?> RunCoreAsync(string name, int year, Guid? actor, bool force, CancellationToken cancellationToken, ClaimsPrincipal? caller = null)
    {
        if (database.ChangeTracker.HasChanges()) throw new InvalidOperationException("SQL job execution requires a dedicated clean scope.");
        if (!SqlJobSchedule.Names.Contains(name, StringComparer.Ordinal) || year != FantacalcioTime.SeasonYearAt(clock.GetUtcNow().UtcDateTime))
            throw new ApplicationFault(400, "invalid_sql_job", "Job non valido o stagione non corrente.");
        var now = clock.GetUtcNow(); var id = Guid.NewGuid();
        await using (var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken))
        {
            if (caller != null) await operations.AdministratorAsync(caller, cancellationToken);
            if (!await database.Seasons.AnyAsync(x => x.SeasonIndex == year, cancellationToken)) database.Seasons.Add(new() { SeasonIndex = year, StartYear = year + 2011 });
            var state = await database.SqlJobStates.SingleOrDefaultAsync(x => x.Name == name && x.SeasonIndex == year, cancellationToken);
            if (state == null) database.SqlJobStates.Add(state = new() { Name = name, SeasonIndex = year,
                NextRunAtUtc = name is "calendar" or "catalogue" ? now : SqlJobSchedule.Next(name, now.AddMinutes(-1)) });
            if (state.LeaseUntilUtc > now)
            {
                if (force) throw new ApplicationFault(409, "sql_job_running", "Un'altra istanza sta già eseguendo questo job.");
                return null;
            }
            if (!force && state.NextRunAtUtc > now)
            {
                if (database.Entry(state).State == EntityState.Added) { await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
                return null;
            }
            if (state.LeaseId is { } previous)
            {
                var abandoned = await database.SqlJobRuns.SingleOrDefaultAsync(x => x.Id == previous && x.Status == "Running", cancellationToken);
                if (abandoned != null) { abandoned.Status = "LeaseLost"; abandoned.Code = "previous_lease_expired"; abandoned.CompletedAtUtc = now; }
            }
            state.LeaseId = id; state.LeaseUntilUtc = now.AddMinutes(5); state.LastStartedAtUtc = now; state.LastStatus = "Running";
            database.SqlJobRuns.Add(new() { Id = id, Name = name, SeasonIndex = year, ActorUserId = actor, StartedAtUtc = now, Status = "Running" });
            await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        }
        database.ChangeTracker.Clear();
        var status = "Succeeded"; var work = new SqlJobWorkResult(0, 0);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(TimeSpan.FromMinutes(4));
        try
        {
            using var fence = SqlJobLeaseFence.Enter(name, year, id, clock.GetUtcNow);
            work = await actions.ExecuteAsync(name, year, timeout.Token).WaitAsync(timeout.Token);
            if (work.Skipped > 0 || work.HasMore) status = "Partial";
        }
        catch (OperationCanceledException) { status = "Canceled"; work = new(0, 0, cancellationToken.IsCancellationRequested ? "job_canceled" : "job_timeout"); }
        catch (ApplicationFault fault) { status = "Failed"; work = new(0, 0, fault.Code); }
        catch (Exception error)
        {
            status = "Failed"; work = new(0, 0, "job_execution_failed");
            logger.LogError("SQL job {JobName} failed ({FailureType}); run {RunId}. No exception message or payload logged.", name, error.GetType().Name, id);
        }
        using var receiptTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using (var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, receiptTimeout.Token))
        {
            var state = await database.SqlJobStates.SingleAsync(x => x.Name == name && x.SeasonIndex == year, receiptTimeout.Token);
            var run = await database.SqlJobRuns.SingleAsync(x => x.Id == id, receiptTimeout.Token);
            if (state.LeaseId != id) return new(id, name, year, "LeaseLost", work.Processed, work.Skipped, "lease_replaced");
            state.LeaseId = null; state.LeaseUntilUtc = null; state.LastStatus = status; state.LastCompletedAtUtc = clock.GetUtcNow();
            state.NextRunAtUtc = status is "Failed" or "Canceled" ? clock.GetUtcNow().AddMinutes(5) : work.HasMore ? clock.GetUtcNow().AddMinutes(1) : SqlJobSchedule.Next(name, clock.GetUtcNow());
            run.Status = status; run.Processed = work.Processed; run.Skipped = work.Skipped; run.Code = work.Code; run.CompletedAtUtc = clock.GetUtcNow();
            await database.SaveChangesAsync(receiptTimeout.Token); await transaction.CommitAsync(receiptTimeout.Token);
        }
        return new(id, name, year, status, work.Processed, work.Skipped, work.Code);
    }
}

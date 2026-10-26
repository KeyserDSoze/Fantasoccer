using System.Security.Claims;
using System.Text.RegularExpressions;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Infrastructure.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Application.Core.Operations;

public sealed record OperationalLogDto(Guid Id, string PrimaryKey, string SecondaryKey, DateTimeOffset? Timestamp, string Message, string Error, string Source);
public sealed record OperationalLogStatistics(int Total, int Errors, int Info, int Today, int LastHour);
public sealed class OperationsApplicationService(FantasoccerDbContext database, AccountApplicationService accounts, TimeProvider clock)
{
    public async Task<OperationalLogDto[]> LogsAsync(ClaimsPrincipal caller, int limit = 100, bool? errorsOnly = null, string? query = null, CancellationToken cancellationToken = default)
    {
        await AdministratorAsync(caller, cancellationToken);
        limit = Math.Clamp(limit, 1, 500);
        if (query?.Length > 200) throw new ApplicationFault(400, "invalid_log_query", "Ricerca troppo lunga.");
        var logs = database.ArchivedOperationalLogs.AsNoTracking().AsQueryable();
        var jobs = database.SqlJobRuns.AsNoTracking().AsQueryable();
        if (errorsOnly is { } error)
        {
            logs = logs.Where(x => (x.PartitionKey == "AError" || x.Error != null && x.Error != "") == error);
            jobs = jobs.Where(x => (x.Status == "Failed" || x.Status == "LeaseLost") == error);
        }
        if (!string.IsNullOrWhiteSpace(query))
        {
            var text = query.ToLowerInvariant();
            logs = logs.Where(x => (x.Message ?? "").ToLower().Contains(text) || (x.Error ?? "").ToLower().Contains(text) || x.PartitionKey.ToLower().Contains(text) || x.RowKey.ToLower().Contains(text));
            jobs = jobs.Where(x => x.Name.ToLower().Contains(text) || (x.Code ?? "").ToLower().Contains(text) || x.Status.ToLower().Contains(text));
        }
        var archive = await logs.OrderByDescending(x => x.TimestampUtc).Take(limit).Select(x => new OperationalLogDto(x.Id, x.PartitionKey, x.RowKey, x.TimestampUtc,
            x.Message ?? "Log legacy archiviato; proprietà originali conservate nel dump e nell'archivio privato.", x.Error ?? "", "legacy-table")).ToArrayAsync(cancellationToken);
        var runs = await jobs.OrderByDescending(x => x.StartedAtUtc).Take(limit).Select(x => new OperationalLogDto(x.Id, x.Status == "Failed" || x.Status == "LeaseLost" ? "AError" : "ALog",
            x.Name + "_" + x.Id, x.CompletedAtUtc ?? x.StartedAtUtc, x.Name + ": " + x.Status + "; elaborati=" + x.Processed + "; saltati=" + x.Skipped, x.Code ?? "", "sql-job")).ToArrayAsync(cancellationToken);
        return archive.Concat(runs).OrderByDescending(x => x.Timestamp).Take(limit).Select(Redact).ToArray();
    }
    public async Task<OperationalLogDto> LogAsync(ClaimsPrincipal caller, Guid id, CancellationToken cancellationToken = default)
    {
        await AdministratorAsync(caller, cancellationToken);
        var log = await database.ArchivedOperationalLogs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (log != null) return Redact(new(log.Id, log.PartitionKey, log.RowKey, log.TimestampUtc, log.Message ?? "Log legacy archiviato.", log.Error ?? "", "legacy-table"));
        var job = await database.SqlJobRuns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new ApplicationFault(404, "log_not_found", "Log non trovato.");
        return new(job.Id, job.Status == "Failed" ? "AError" : "ALog", job.Name + "_" + job.Id, job.CompletedAtUtc ?? job.StartedAtUtc, $"{job.Name}: {job.Status}; elaborati={job.Processed}; saltati={job.Skipped}", job.Code ?? "", "sql-job");
    }
    public async Task<OperationalLogStatistics> StatisticsAsync(ClaimsPrincipal caller, CancellationToken cancellationToken = default)
    {
        await AdministratorAsync(caller, cancellationToken);
        var now = clock.GetUtcNow(); var today = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero); var hour = now.AddHours(-1);
        var total = await database.ArchivedOperationalLogs.CountAsync(cancellationToken) + await database.SqlJobRuns.CountAsync(cancellationToken);
        var errors = await database.ArchivedOperationalLogs.CountAsync(x => x.PartitionKey == "AError" || x.Error != null && x.Error != "", cancellationToken) + await database.SqlJobRuns.CountAsync(x => x.Status == "Failed" || x.Status == "LeaseLost", cancellationToken);
        return new(total, errors, total - errors,
            await database.ArchivedOperationalLogs.CountAsync(x => x.TimestampUtc >= today, cancellationToken) + await database.SqlJobRuns.CountAsync(x => x.StartedAtUtc >= today, cancellationToken),
            await database.ArchivedOperationalLogs.CountAsync(x => x.TimestampUtc >= hour, cancellationToken) + await database.SqlJobRuns.CountAsync(x => x.StartedAtUtc >= hour, cancellationToken));
    }
    public async Task<AccountIdentityDto> AdministratorAsync(ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var actor = await accounts.CurrentAsync(caller, cancellationToken);
        if (!caller.IsInRole("super_admin") || !accounts.IsGlobalAdministrator(actor.Email)) throw new ApplicationFault(403, "operations_admin_required", "Serve un amministratore globale per consultare i log e avviare job globali.");
        return actor;
    }
    private static OperationalLogDto Redact(OperationalLogDto row) => row with { Message = SafeText(row.Message), Error = SafeText(row.Error) };
    public static string SafeText(string text)
    {
        if (text.Length > 16000) text = text[..16000] + " [truncated; original retained in private archive]";
        text = Regex.Replace(text, @"https?://[^\s<>""']+", "[URL redacted]", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
        return Regex.Replace(text, @"(?i)(authorization|password|client_secret|api[_-]?key|private[_-]?key|access_token|refresh_token|deviceproof)\s*[:=]\s*[^\s,;]+", "$1=[redacted]", RegexOptions.None, TimeSpan.FromSeconds(1));
    }
}

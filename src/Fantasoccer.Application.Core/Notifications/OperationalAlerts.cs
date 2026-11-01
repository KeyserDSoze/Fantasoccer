using System.Text.RegularExpressions;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Operations;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fantasoccer.Application.Core.Notifications;

public static class OperationalAlertText
{
    // Only internal codes, never exception messages or provider response bodies.
    public static string SafeCode(string? value) => value is { Length: > 0 and <= 100 } &&
        Regex.IsMatch(value, "^[a-zA-Z0-9_-]+$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)) ? value : "operation_failed";
}

public sealed class OperationalFailureRecorder(IServiceScopeFactory scopes, TimeProvider clock, ILogger<OperationalFailureRecorder> logger)
{
    public async Task RecordAsync(string source, string? code)
    {
        // Independent context/token: an import's rolled-back transaction and its
        // canceled request must not erase the receipt. Failure to log is best effort.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<FantasoccerDbContext>();
            await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, timeout.Token);
            database.OperationalFailures.Add(new() { Id = Guid.NewGuid(), Source = OperationalAlertText.SafeCode(source), Code = OperationalAlertText.SafeCode(code), OccurredAtUtc = clock.GetUtcNow() });
            await database.SaveChangesAsync(timeout.Token); await transaction.CommitAsync(timeout.Token);
        }
        catch (Exception error) { logger.LogWarning("Operational failure receipt unavailable ({FailureType}); no original payload logged.", error.GetType().Name); }
    }
}

public sealed class OperationalAlertProducer(FantasoccerDbContext database, AccountApplicationService accounts, PushOutboxApplicationService outbox, TimeProvider clock)
{
    public async Task<int> RunAsync(int year, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow(); var cutoff = now.AddHours(-24);
        var recipients = await (from preference in database.UserPushPreferences.AsNoTracking()
            join user in database.Users.AsNoTracking() on preference.UserId equals user.Id
            where preference.OperationalAlerts && preference.OperationalAlertsEnabledAtUtc != null && user.IsEnabled && user.Email != null
            select new { user.Id, user.Email, Since = preference.OperationalAlertsEnabledAtUtc!.Value }).ToArrayAsync(cancellationToken);
        recipients = recipients.Where(x => accounts.IsGlobalAdministrator(x.Email)).ToArray();
        if (recipients.Length == 0) return 0;
        var jobs = await database.SqlJobRuns.AsNoTracking().Where(x => x.CompletedAtUtc >= cutoff && x.CompletedAtUtc <= now &&
            (x.Status == "Failed" || x.Status == "LeaseLost" || x.Status == "Canceled" && x.Code == "job_timeout" ||
             x.Status == "Partial" && x.Code != null && !SqlJobOutcomes.ExpectedWaitCodes.Contains(x.Code)))
            .OrderByDescending(x => x.CompletedAtUtc).Take(200)
            .Select(x => new { Source = "job_" + x.Name, x.Code, At = x.CompletedAtUtc!.Value }).ToArrayAsync(cancellationToken);
        var imports = await database.OperationalFailures.AsNoTracking().Where(x => x.OccurredAtUtc >= cutoff && x.OccurredAtUtc <= now)
            .OrderByDescending(x => x.OccurredAtUtc).Take(200).Select(x => new { x.Source, Code = (string?)x.Code, At = x.OccurredAtUtc }).ToArrayAsync(cancellationToken);
        var events = jobs.Concat(imports).ToArray();
        if (events.Length == 0) return 0;
        var queued = 0;
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        if (!await database.Seasons.AnyAsync(x => x.SeasonIndex == year, cancellationToken)) database.Seasons.Add(new() { SeasonIndex = year, StartYear = year + 2011 });
        foreach (var recipient in recipients)
        {
            // Recheck after taking the write lock; a concurrent opt-out wins.
            var preference = await database.UserPushPreferences.AsNoTracking().SingleAsync(x => x.UserId == recipient.Id, cancellationToken);
            if (!preference.OperationalAlerts || preference.OperationalAlertsEnabledAtUtc == null) continue;
            foreach (var failure in events.Where(x => x.At >= preference.OperationalAlertsEnabledAtUtc).GroupBy(x => new
            {
                Source = OperationalAlertText.SafeCode(x.Source), Code = OperationalAlertText.SafeCode(x.Code),
                Window = x.At.UtcTicks / TimeSpan.FromMinutes(30).Ticks
            }))
            {
                var key = $"{failure.Key.Source}:{failure.Key.Code}:{failure.Key.Window}";
                if (await outbox.EnqueueOperationalAsync(recipient.Id, year, key,
                    $"{failure.Key.Source}: {failure.Key.Code}. Controlla i log amministrativi.", cancellationToken)) queued++;
            }
        }
        await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        return queued;
    }
}

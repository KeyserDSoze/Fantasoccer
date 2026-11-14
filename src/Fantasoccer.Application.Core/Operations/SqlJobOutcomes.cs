namespace Fantasoccer.Application.Core.Operations;

public static class SqlJobOutcomes
{
    public static readonly string[] ExpectedWaitCodes = ["results_waiting_for_official_data", "postponed_matchdays_pending", "hall_batch_pending", "official_votes_pending", "live_votes_closed", "chances_lineups_pending"];

    // Incidents that need a person (the hall baseline must be confirmed): retried every hour instead of every minute.
    public static readonly string[] SlowRetryCodes = ["hall_baseline_reconciliation_required"];

    // The provider refuses us (forbidden, unauthorized, gone, blocked for legal reasons): asking again a minute later cannot help, the alert is what matters.
    public static bool IsPermanentHttpRefusal(Exception? error)
    {
        for (var current = error; current is not null; current = current.InnerException)
            if (current is HttpRequestException { StatusCode: { } status } && status is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.Gone or System.Net.HttpStatusCode.UnavailableForLegalReasons) return true;
        return false;
    }

    public static bool IsIncident(string status, string? code) => status is "Failed" or "LeaseLost" ||
        status == "Canceled" && code == "job_timeout" ||
        status == "Partial" && code != null && !ExpectedWaitCodes.Contains(code, StringComparer.Ordinal);
}

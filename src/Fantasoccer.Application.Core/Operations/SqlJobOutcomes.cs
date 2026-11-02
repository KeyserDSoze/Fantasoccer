namespace Fantasoccer.Application.Core.Operations;

public static class SqlJobOutcomes
{
    public static readonly string[] ExpectedWaitCodes = ["results_waiting_for_official_data", "postponed_matchdays_pending", "hall_batch_pending", "official_votes_pending"];

    public static bool IsIncident(string status, string? code) => status is "Failed" or "LeaseLost" ||
        status == "Canceled" && code == "job_timeout" ||
        status == "Partial" && code != null && !ExpectedWaitCodes.Contains(code, StringComparer.Ordinal);
}

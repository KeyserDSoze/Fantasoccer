using Fantasoccer.Application.Core.Operations;

namespace Fantasoccer.Tests.Tests.Rules;

public sealed class SqlJobOutcomesTest
{
    [Theory]
    [InlineData("Failed", "provider_failed", true)]
    [InlineData("LeaseLost", "previous_lease_expired", true)]
    [InlineData("Canceled", "job_timeout", true)]
    [InlineData("Canceled", "job_canceled", false)]
    [InlineData("Partial", "league_calculation_failed", true)]
    [InlineData("Partial", "results_waiting_for_official_data", false)]
    [InlineData("Partial", "postponed_matchdays_pending", false)]
    [InlineData("Partial", "hall_batch_pending", false)]
    [InlineData("Partial", null, false)]
    [InlineData("Succeeded", null, false)]
    public void Only_actual_failures_are_operational_incidents(string status, string? code, bool expected)
        => Assert.Equal(expected, SqlJobOutcomes.IsIncident(status, code));
}

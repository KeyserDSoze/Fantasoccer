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
    [InlineData("Partial", "hall_baseline_reconciliation_required", true)]
    [InlineData("Partial", "chances_lineups_pending", false)]
    [InlineData("Partial", null, false)]
    [InlineData("Succeeded", null, false)]
    public void Only_actual_failures_are_operational_incidents(string status, string? code, bool expected)
        => Assert.Equal(expected, SqlJobOutcomes.IsIncident(status, code));

    [Fact]
    public void A_person_is_needed_for_the_hall_baseline_so_it_is_not_retried_every_minute()
        => Assert.Contains("hall_baseline_reconciliation_required", SqlJobOutcomes.SlowRetryCodes);

    [Fact]
    public void A_database_timeout_is_explained_without_exposing_the_exception_text()
    {
        var detail = Fantasoccer.Application.Core.Notifications.OperationalAlertText.ExceptionDetail(new InvalidOperationException("password=secret", new TimeoutException("Exception while reading from stream")));
        Assert.StartsWith("Eccezione InvalidOperationException ← TimeoutException", detail);
        Assert.Contains("120 secondi", detail);
        Assert.DoesNotContain("secret", detail);
        Assert.Equal("Eccezione HttpRequestException", Fantasoccer.Application.Core.Notifications.OperationalAlertText.ExceptionDetail(new HttpRequestException("x")));
    }

    [Theory]
    [InlineData(System.Net.HttpStatusCode.Forbidden, true)]
    [InlineData(System.Net.HttpStatusCode.Unauthorized, true)]
    [InlineData(System.Net.HttpStatusCode.Gone, true)]
    [InlineData(System.Net.HttpStatusCode.InternalServerError, false)]
    [InlineData(System.Net.HttpStatusCode.TooManyRequests, false)]
    public void A_provider_refusal_is_not_retried_but_a_temporary_error_is(System.Net.HttpStatusCode status, bool refusal)
    {
        Assert.Equal(refusal, SqlJobOutcomes.IsPermanentHttpRefusal(new HttpRequestException("x", null, status)));
        Assert.Equal(refusal, SqlJobOutcomes.IsPermanentHttpRefusal(new InvalidOperationException("y", new HttpRequestException("x", null, status))));
    }

    [Fact]
    public void A_network_error_without_status_is_retried() => Assert.False(SqlJobOutcomes.IsPermanentHttpRefusal(new HttpRequestException("x")));
}

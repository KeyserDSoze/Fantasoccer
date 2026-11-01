using System.Diagnostics;
using Fantasoccer.Application.Api.Observability;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed class TelemetryPrivacyTests
{
    [Fact]
    public void Database_spans_keep_timing_and_operation_but_not_sql_text()
    {
        using var activity = new Activity("postgres.query").Start();
        activity.SetTag("db.statement", "SELECT * FROM users WHERE email = 'private@example.test'");
        activity.SetTag("db.query.text", "SELECT 'secret'");
        activity.SetTag("db.operation.name", "SELECT");
        new SensitiveSpanProcessor().OnEnd(activity);
        Assert.Null(activity.GetTagItem("db.statement"));
        Assert.Null(activity.GetTagItem("db.query.text"));
        Assert.Equal("SELECT", activity.GetTagItem("db.operation.name"));
    }
}

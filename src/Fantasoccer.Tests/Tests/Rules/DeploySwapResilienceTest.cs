using Fantasoccer.Application.Api.Operations;
using Fantasoccer.Application.Core.Operations;
using Fantasoccer.Infrastructure.PostgreSql;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Fantasoccer.Tests.Tests.Rules;

public sealed class DeploySwapResilienceTest
{
    [Fact]
    public async Task Background_work_waits_past_the_deploy_swap_and_stops_cleanly_if_the_application_stops_first()
    {
        Assert.Equal(300, new JobMaintenanceOptions().StartDelaySeconds);
        Assert.True(await BackgroundStart.WaitAsync(0, NullLogger.Instance, "test", CancellationToken.None));
        using var stopping = new CancellationTokenSource(); stopping.Cancel();
        Assert.False(await BackgroundStart.WaitAsync(300, NullLogger.Instance, "test", stopping.Token));
    }

    [Fact]
    public void Database_connections_use_TCP_keepalive_unless_configured_otherwise()
    {
        var configured = new NpgsqlConnectionStringBuilder(AbandonedSessionReaper.WithApplicationName("Host=db;Database=x;Username=u;Password=p"));
        Assert.True(configured.TcpKeepAlive); Assert.Equal(10, configured.TcpKeepAliveTime); Assert.Equal(5, configured.TcpKeepAliveInterval);
        Assert.StartsWith(AbandonedSessionReaper.ApplicationNamePrefix, configured.ApplicationName);
        var explicitOne = new NpgsqlConnectionStringBuilder(AbandonedSessionReaper.WithApplicationName("Host=db;Tcp Keepalive=true;Tcp Keepalive Time=60;Tcp Keepalive Interval=20"));
        Assert.Equal(60, explicitOne.TcpKeepAliveTime);
    }
}

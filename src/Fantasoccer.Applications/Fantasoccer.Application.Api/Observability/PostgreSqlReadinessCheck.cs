using Fantasoccer.Infrastructure.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Fantasoccer.Application.Api.Observability;

public sealed class PostgreSqlReadinessCheck(FantasoccerDbContext database) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await database.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("PostgreSQL is unavailable.");
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Do not expose connection strings or provider error details in health responses.
            return HealthCheckResult.Unhealthy("PostgreSQL is unavailable.");
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fantasoccer.Infrastructure.PostgreSql;

public sealed class FantasoccerDbContextFactory : IDesignTimeDbContextFactory<FantasoccerDbContext>
{
    public FantasoccerDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("FANTASOCCER_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Set FANTASOCCER_POSTGRES_CONNECTION explicitly for EF tooling.");
        return new FantasoccerDbContext(new DbContextOptionsBuilder<FantasoccerDbContext>()
            .UseNpgsql(connectionString, postgres => postgres.CommandTimeout(120)).Options);
    }
}

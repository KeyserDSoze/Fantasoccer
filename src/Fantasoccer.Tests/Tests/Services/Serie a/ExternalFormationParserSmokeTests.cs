using System.Runtime.CompilerServices;
using Fantasoccer.Business;
using Fantasoccer.Business.IngestionData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Tests.Tests.Services;

[Trait("Category", "External")]
public sealed class ExternalFormationParserSmokeTests : IDisposable
{
    private readonly ServiceProvider _services;
    private readonly IFactory<IFormationParser> _factory;

    public ExternalFormationParserSmokeTests()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SocialLogin:Microsoft:ClientId"] = "test-client-id",
                ["SocialLogin:Microsoft:ClientSecret"] = "test-client-secret",
                ["SocialLogin:Microsoft:RedirectDomains"] = "https://localhost"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddFantasoccerBusiness(configuration);
        _services = services.BuildServiceProvider();
        _factory = _services.GetRequiredService<IFactory<IFormationParser>>();
    }

    public void Dispose()
        => _services.Dispose();

    [ExternalFact]
    public async Task SkyReturnsCurrentProbableLineups()
        => await AssertProviderAsync("Sky");

    [ExternalFact]
    public async Task MediasetReturnsCurrentProbableLineups()
        => await AssertProviderAsync("Mediaset");

    private async Task AssertProviderAsync(string name)
    {
        var parser = _factory.Create(name) ?? throw new InvalidOperationException($"Parser {name} is not registered.");

        var players = await parser.GetPlayersAsync();

        Assert.True(players.Count >= 100, $"{name} returned only {players.Count} probable starters.");
        Assert.True(players.Select(x => x.RealTeam.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() >= 10,
            $"{name} returned fewer than 10 teams.");
    }
}

public sealed class ExternalFactAttribute : FactAttribute
{
    public ExternalFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_EXTERNAL_PARSER_TESTS"), "true", StringComparison.OrdinalIgnoreCase))
            Skip = "Set RUN_EXTERNAL_PARSER_TESTS=true to call external providers.";
    }
}

using System.Runtime.CompilerServices;
using System.Text.Json;
using Fantasoccer.Business;
using Fantasoccer.Business.IngestionData;
using Fantasoccer.Business.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Tests.Tests.Services;

// Opt-in only: public-source reads, never real storage writes, SQL, credentials or user data.
[Trait("Category", "External")]
public sealed class ExternalFootballSmokeTests
{
    [ExternalTheory]
    [InlineData("Fantagazzetta")]
    [InlineData("Gazzetta")]
    [InlineData("Injury")]
    public async Task Active_formation_sources_return_recognizable_players(string name)
    {
        var services = new ServiceCollection();
        services.AddFantasoccerSqlDataProviders();
        await using var provider = services.BuildServiceProvider();
        var parser = provider.GetRequiredService<IFactory<IFormationParser>>().Create(name)!;
        var players = await parser.GetPlayersAsync().WaitAsync(TimeSpan.FromSeconds(90), TestContext.Current.CancellationToken);
        Assert.NotEmpty(players);
        Assert.All(players, player =>
        {
            Assert.False(string.IsNullOrWhiteSpace(player.Name));
            Assert.NotNull(player.Chance);
        });
    }

    [ExternalFact]
    public async Task Official_photo_can_be_reloaded_through_the_real_HttpClient_configuration_without_writing_to_Azure()
    {
        var services = new ServiceCollection();
        services.AddFantasoccerSqlDataProviders();
        services.AddLock();
        await using var provider = services.BuildServiceProvider();
        var clients = provider.GetRequiredService<IHttpClientFactory>();
        var client = clients.CreateClient("player-images");
        var cancellation = TestContext.Current.CancellationToken;
        using var seasons = JsonDocument.Parse(await client.GetStringAsync("competitions/serie-a::Football_Competition::ec93b94f74294dc98ab5bcfd67fc0d88/seasons?locale=it-IT", cancellation));
        var season = seasons.RootElement.GetProperty("seasons").EnumerateArray()
            .First(x => x.GetProperty("seasonName").GetString() == FantacalcioTime.LabelYear(FantacalcioTime.ActualYear));
        using var catalogue = JsonDocument.Parse(await client.GetStringAsync($"seasons/{Uri.EscapeDataString(season.GetProperty("seasonId").GetString()!)}/stats/players?category=General&page=1&locale=it-IT", cancellation));
        var sample = catalogue.RootElement.GetProperty("players").EnumerateArray().First(x =>
            x.TryGetProperty("imagery", out var imagery) && imagery.ValueKind == JsonValueKind.Object &&
            imagery.EnumerateObject().Any(p => p.Value.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(p.Value.GetString())));
        var player = new RealPlayer { Name = sample.GetProperty("displayName").GetString()!, Role = (Role)(sample.GetProperty("role").GetInt32() - 1),
            RealTeam = new() { Name = sample.GetProperty("team").GetProperty("officialName").GetString()! }, IsActive = true };
        var store = new PhotoSink();
        var refresher = new PlayerImageService(clients, new UnusedCatalogue(), store, provider.GetRequiredService<System.Threading.Concurrent.ILock>());
        // Exercise the same end-to-end budget as POST /api/player-images/refresh, not just per-request timeouts.
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        budget.CancelAfter(TimeSpan.FromSeconds(45));
        Assert.True(await refresher.ReloadAsync(player, budget.Token));
        Assert.True(store.Length > 0);
    }

    private sealed class PhotoSink : IPlayerImageStore
    {
        public int Length { get; private set; }
        public Task<bool> ExistsAsync(string name, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task UploadAsync(string name, byte[] image, string contentType, string cacheControl, CancellationToken cancellationToken)
        { Length = image.Length; return Task.CompletedTask; }
    }
    private sealed class UnusedCatalogue : IFootballIngestionReader
    {
        public Task<RealCalendar?> CalendarAsync(int year) => throw new InvalidOperationException("No SQL is used by this probe.");
        public Task<RealPlayersWrapper?> PlayersAsync(int year) => throw new InvalidOperationException("No SQL is used by this probe.");
        public Task<RealTeamWrapper?> ClubsAsync(int year) => throw new InvalidOperationException("No SQL is used by this probe.");
    }
}

public sealed class ExternalFactAttribute : FactAttribute
{
    public ExternalFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1) : base(sourceFilePath, sourceLineNumber)
    {
        if (Environment.GetEnvironmentVariable("RUN_EXTERNAL_PARSER_TESTS") != "true") Skip = "Set RUN_EXTERNAL_PARSER_TESTS=true to read the public football sources.";
    }
}
public sealed class ExternalTheoryAttribute : TheoryAttribute
{
    public ExternalTheoryAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1) : base(sourceFilePath, sourceLineNumber)
    {
        if (Environment.GetEnvironmentVariable("RUN_EXTERNAL_PARSER_TESTS") != "true") Skip = "Set RUN_EXTERNAL_PARSER_TESTS=true to read the public football sources.";
    }
}

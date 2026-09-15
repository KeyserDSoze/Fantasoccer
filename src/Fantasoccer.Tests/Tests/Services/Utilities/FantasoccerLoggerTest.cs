using System.Reflection;
using Fantasoccer.Business;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;
using RepositoryFramework.InMemory;

namespace Fantasoccer.Tests.Tests.Services;

[Collection("InMemoryRepository")]
public sealed class FantasoccerLoggerTest : IAsyncLifetime, IDisposable
{
    private readonly ServiceProvider _services;
    private readonly IFantasoccerLogger _logger;
    private readonly IRepository<FantasoccerLog, string> _logs;
    private FantasoccerLog? _logSnapshot;
    private FantasoccerLog? _errorSnapshot;

    public FantasoccerLoggerTest()
    {
        var services = new ServiceCollection();
        services.AddRepository<FantasoccerLog, string>(builder => builder.WithInMemory());
        services.AddSharedBusiness();

        _services = services.BuildServiceProvider();
        _logger = _services.GetRequiredService<IFantasoccerLogger>();
        _logs = _services.GetRequiredService<IRepository<FantasoccerLog, string>>();
    }

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _logSnapshot = await _logs.GetAsync(LogId, cancellationToken);
        _errorSnapshot = await _logs.GetAsync(ErrorId, cancellationToken);
        await _logs.DeleteAsync(LogId, cancellationToken);
        await _logs.DeleteAsync(ErrorId, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _logs.DeleteAsync(LogId, cancellationToken);
        await _logs.DeleteAsync(ErrorId, cancellationToken);
        if (_logSnapshot != null)
            await _logs.InsertAsync(LogId, _logSnapshot, cancellationToken);
        if (_errorSnapshot != null)
            await _logs.InsertAsync(ErrorId, _errorSnapshot, cancellationToken);
    }

    public void Dispose()
    {
    }

    [Fact]
    public async Task UpdatesExistingLogSlots()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _logs.InsertAsync(LogId, ExistingLog(LogId), cancellationToken);
        await _logs.InsertAsync(ErrorId, ExistingLog(ErrorId), cancellationToken);
        var exception = new InvalidOperationException("Primary failure");

        await _logger.LogAsync<FantasoccerLoggerTest>("Completed");
        await _logger.LogErrorAsync<FantasoccerLoggerTest>(exception);

        var log = await _logs.GetAsync(LogId, cancellationToken);
        var error = await _logs.GetAsync(ErrorId, cancellationToken);
        Assert.NotNull(log);
        Assert.Contains("Completed", log.Message);
        Assert.NotNull(error);
        Assert.Contains("Primary failure", error.Message);
        Assert.Contains(nameof(InvalidOperationException), error.Error);
    }

    [Fact]
    public async Task DoesNotPropagateRepositoryFailures()
    {
        var repository = DispatchProxy.Create<IRepository<FantasoccerLog, string>, ThrowingLogRepositoryProxy>();
        var services = new ServiceCollection();
        services.AddSingleton(repository);
        services.AddSharedBusiness();
        using var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<IFantasoccerLogger>();

        await logger.LogAsync<FantasoccerLoggerTest>("Completed");
        await logger.LogErrorAsync<FantasoccerLoggerTest>(new InvalidOperationException("Primary failure"));
    }

    private static FantasoccerLog ExistingLog(string id)
        => new()
        {
            PrimaryKey = "existing",
            SecondaryKey = id,
            Message = "existing"
        };

    private static string LogId
        => $"{IFantasoccerLogger.LogPrimaryKey}_-_{nameof(FantasoccerLoggerTest)}_{DateTime.UtcNow.Hour}";

    private static string ErrorId
        => $"{IFantasoccerLogger.ErrorPrimaryKey}_-_{nameof(FantasoccerLoggerTest)}_{DateTime.UtcNow.Hour}";
}

public class ThrowingLogRepositoryProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        => throw new InvalidOperationException("503 ServerBusy");
}

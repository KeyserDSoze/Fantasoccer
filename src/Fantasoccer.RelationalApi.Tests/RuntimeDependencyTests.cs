using System.Text.Json;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed class RuntimeDependencyTests
{
    [Fact]
    public void Api_dependency_graph_has_only_the_allowed_Rystem_packages_and_never_the_migration_models()
    {
        using var graph = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fantasoccer.Application.Api.deps.json")));
        var names = graph.RootElement.GetProperty("libraries").EnumerateObject().Select(x => x.Name.Split('/')[0]).ToArray();
        var allowed = new[] { "Rystem", "Rystem.DependencyInjection", "Rystem.Concurrency" };
        Assert.All(names.Where(x => x.StartsWith("Rystem", StringComparison.OrdinalIgnoreCase)), name => Assert.Contains(name, allowed));
        Assert.DoesNotContain("Fantasoccer.LegacyModels", names);
        Assert.DoesNotContain("Fantasoccer.Business.BackgroundJobs", names);
        Assert.DoesNotContain("Fantasoccer.Infrastructure.InMemory", names);
        Assert.Contains("Rystem.Concurrency", names);
        Assert.Contains("Rystem.DependencyInjection", names);
    }
}

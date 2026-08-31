using ELifeRPG.Bridge.Api.Services;
using Xunit;

namespace ELifeRPG.Bridge.Api.UnitTests.Services;

public sealed class DependencyHealthCacheTests
{
    [Fact]
    public void Current_BeforeAnyPublish_ListsEveryProbeAsUnknown()
    {
        var cache = new DependencyHealthCache([new StubProbe("central_api"), new StubProbe("keycloak")]);

        var report = cache.Current;

        Assert.Equal(HealthStatus.Unknown, report.Status);
        Assert.Null(report.CheckedAt);
        Assert.Equal(["central_api", "keycloak"], report.Dependencies.Select(dependency => dependency.Name));
        Assert.All(report.Dependencies, dependency => Assert.Equal(HealthStatus.Unknown, dependency.Status));
        Assert.All(report.Dependencies, dependency => Assert.Null(dependency.DurationMs));
    }

    [Fact]
    public void Publish_ReplacesTheWholeReport()
    {
        var cache = new DependencyHealthCache([new StubProbe("central_api")]);
        var published = new HealthReport(
            HealthStatus.Healthy,
            DateTimeOffset.UtcNow,
            [new DependencyHealth("central_api", HealthStatus.Healthy, 5, null)]);

        cache.Publish(published);

        Assert.Same(published, cache.Current);
    }
}

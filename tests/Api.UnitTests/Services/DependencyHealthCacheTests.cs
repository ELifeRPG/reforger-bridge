using System.Net;
using ELifeRPG.Bridge.Api.Services;
using Xunit;

namespace ELifeRPG.Bridge.Api.UnitTests.Services;

public sealed class DependencyHealthCacheTests
{
    [Fact]
    public void Current_BeforeAnyPublish_ListsEveryProbeAsUnknown()
    {
        var cache = new DependencyHealthCache([ProbeFactory.Responding("backend", HttpStatusCode.OK), ProbeFactory.Responding("keycloak", HttpStatusCode.OK)]);

        var report = cache.Current;

        Assert.Equal(HealthStatus.Unknown, report.Status);
        Assert.Null(report.CheckedAt);
        Assert.Equal(["backend", "keycloak"], report.Dependencies.Select(dependency => dependency.Name));
        Assert.All(report.Dependencies, dependency => Assert.Equal(HealthStatus.Unknown, dependency.Status));
        Assert.All(report.Dependencies, dependency => Assert.Null(dependency.DurationMs));
    }

    [Fact]
    public void Publish_ReplacesTheWholeReport()
    {
        var cache = new DependencyHealthCache([ProbeFactory.Responding("backend", HttpStatusCode.OK)]);
        var published = new HealthReport(
            HealthStatus.Healthy,
            DateTimeOffset.UtcNow,
            [new DependencyHealth("backend", HealthStatus.Healthy, 5, null)]);

        cache.Publish(published);

        Assert.Same(published, cache.Current);
    }
}

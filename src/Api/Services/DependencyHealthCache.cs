namespace ELifeRPG.Bridge.Api.Services;

public sealed class DependencyHealthCache
{
    private volatile HealthReport _current;

    public DependencyHealthCache(IEnumerable<HttpDependencyProbe> probes)
        => _current = new HealthReport(
            HealthStatus.Unknown,
            null,
            [.. probes.Select(probe => new DependencyHealth(probe.Name, HealthStatus.Unknown, null, null))]);

    public HealthReport Current => _current;

    public void Publish(HealthReport report) => _current = report;
}

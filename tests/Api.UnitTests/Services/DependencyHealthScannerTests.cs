using ELifeRPG.Bridge.Api.Configuration;
using ELifeRPG.Bridge.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ELifeRPG.Bridge.Api.UnitTests.Services;

public sealed class DependencyHealthScannerTests
{
    private static (DependencyHealthScanner Scanner, DependencyHealthCache Cache) Build(
        IDependencyProbe[] probes,
        TimeSpan? probeTimeout = null)
    {
        var cache = new DependencyHealthCache(probes);
        var options = Options.Create(new DependencyHealthOptions
        {
            ProbeTimeout = probeTimeout ?? TimeSpan.FromSeconds(10),
        });

        return (new DependencyHealthScanner(probes, cache, options, NullLogger<DependencyHealthScanner>.Instance), cache);
    }

    [Fact]
    public async Task ScanOnceAsync_PublishesOneEntryPerProbe_InRegistrationOrder()
    {
        var (scanner, cache) = Build([
            new StubProbe("backend", new ProbeOutcome(HealthStatus.Healthy, null)),
            new StubProbe("keycloak", new ProbeOutcome(HealthStatus.Healthy, null)),
        ]);

        await scanner.ScanOnceAsync(CancellationToken.None);

        Assert.Equal(["backend", "keycloak"], cache.Current.Dependencies.Select(dependency => dependency.Name));
    }

    [Fact]
    public async Task ScanOnceAsync_SetsCheckedAt()
    {
        var (scanner, cache) = Build([new StubProbe("backend", new ProbeOutcome(HealthStatus.Healthy, null))]);

        await scanner.ScanOnceAsync(CancellationToken.None);

        Assert.NotNull(cache.Current.CheckedAt);
    }

    [Fact]
    public async Task ScanOnceAsync_AggregatesTheOverallStatus()
    {
        var (scanner, cache) = Build([
            new StubProbe("backend", new ProbeOutcome(HealthStatus.Healthy, null)),
            new StubProbe("keycloak", new ProbeOutcome(HealthStatus.Unhealthy, "down")),
        ]);

        await scanner.ScanOnceAsync(CancellationToken.None);

        Assert.Equal(HealthStatus.Degraded, cache.Current.Status);
    }

    [Fact]
    public async Task ScanOnceAsync_WhenAProbeThrows_ReportsThatDependencyUnhealthyAndStillReportsTheOthers()
    {
        var (scanner, cache) = Build([
            new ThrowingProbe("backend", new InvalidOperationException("boom")),
            new StubProbe("keycloak", new ProbeOutcome(HealthStatus.Healthy, null)),
        ]);

        await scanner.ScanOnceAsync(CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, cache.Current.Dependencies.Single(d => d.Name == "backend").Status);
        Assert.Equal(HealthStatus.Healthy, cache.Current.Dependencies.Single(d => d.Name == "keycloak").Status);
    }

    [Fact]
    public async Task ScanOnceAsync_WhenAProbeHangs_ReportsUnhealthyAfterTheTimeout()
    {
        var (scanner, cache) = Build([new HangingProbe("backend")], TimeSpan.FromMilliseconds(50));

        await scanner.ScanOnceAsync(CancellationToken.None);

        var dependency = cache.Current.Dependencies.Single();
        Assert.Equal(HealthStatus.Unhealthy, dependency.Status);
        Assert.Contains("Did not answer", dependency.Detail);
    }

    [Fact]
    public async Task ScanOnceAsync_WhenShutdownCancelsMidScan_DoesNotPublish()
    {
        var (scanner, cache) = Build([new StubProbe("backend", new ProbeOutcome(HealthStatus.Healthy, null))]);
        var before = cache.Current;

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        try
        {
            await scanner.ScanOnceAsync(cancelled.Token);
        }
        catch (OperationCanceledException)
        {
        }

        Assert.Same(before, cache.Current);
    }
}

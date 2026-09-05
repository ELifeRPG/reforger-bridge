using ELifeRPG.Bridge.Api.Services;
using Xunit;

namespace ELifeRPG.Bridge.Api.UnitTests.Services;

public sealed class HealthStatusesTests
{
    [Fact]
    public void Aggregate_WhenEveryDependencyIsHealthy_ReportsHealthy()
        => Assert.Equal(
            HealthStatus.Healthy,
            HealthStatuses.Aggregate([HealthStatus.Healthy, HealthStatus.Healthy]));

    [Fact]
    public void Aggregate_WhenOneDependencyIsDown_ReportsDegraded()
        => Assert.Equal(
            HealthStatus.Degraded,
            HealthStatuses.Aggregate([HealthStatus.Healthy, HealthStatus.Unhealthy]));

    [Fact]
    public void Aggregate_WhenEveryDependencyIsDown_ReportsUnhealthy()
        => Assert.Equal(
            HealthStatus.Unhealthy,
            HealthStatuses.Aggregate([HealthStatus.Unhealthy, HealthStatus.Unhealthy]));

    [Fact]
    public void Aggregate_BeforeTheFirstScan_ReportsUnknownRatherThanDegraded()
        => Assert.Equal(
            HealthStatus.Unknown,
            HealthStatuses.Aggregate([HealthStatus.Unknown, HealthStatus.Unknown]));

    [Fact]
    public void Aggregate_WithNoDependencies_ReportsUnknown()
        => Assert.Equal(HealthStatus.Unknown, HealthStatuses.Aggregate([]));

    [Theory]
    [InlineData(HealthStatus.Healthy, true)]
    [InlineData(HealthStatus.Degraded, false)]
    [InlineData(HealthStatus.Unhealthy, false)]
    [InlineData(HealthStatus.Unknown, false)]
    public void IsReady_OnlyWhenEveryDependencyIsHealthy(HealthStatus status, bool expected)
        => Assert.Equal(expected, HealthStatuses.IsReady(status));

    [Fact]
    public void Aggregate_WhenOnlySomeDependenciesHaveBeenScanned_ReportsDegraded()
        => Assert.Equal(
            HealthStatus.Degraded,
            HealthStatuses.Aggregate([HealthStatus.Healthy, HealthStatus.Unknown]));
}

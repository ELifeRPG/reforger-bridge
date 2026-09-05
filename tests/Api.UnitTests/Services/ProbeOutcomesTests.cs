using System.Net;
using ELifeRPG.Bridge.Api.Services;
using Xunit;

namespace ELifeRPG.Bridge.Api.UnitTests.Services;

public sealed class ProbeOutcomesTests
{
    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.NoContent)]
    public void FromStatusCode_MapsSuccessToHealthy(HttpStatusCode statusCode)
        => Assert.Equal(HealthStatus.Healthy, ProbeOutcomes.FromStatusCode(statusCode).Status);

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public void FromStatusCode_TreatsUnauthorizedAsHealthy(HttpStatusCode statusCode)
        => Assert.Equal(HealthStatus.Healthy, ProbeOutcomes.FromStatusCode(statusCode).Status);

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public void FromStatusCode_MapsServerErrorsToUnhealthy(HttpStatusCode statusCode)
        => Assert.Equal(HealthStatus.Unhealthy, ProbeOutcomes.FromStatusCode(statusCode).Status);

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadRequest)]
    public void FromStatusCode_MapsUnexpectedClientErrorsToDegraded(HttpStatusCode statusCode)
        => Assert.Equal(HealthStatus.Degraded, ProbeOutcomes.FromStatusCode(statusCode).Status);

    [Fact]
    public void FromException_MapsConnectionFailuresToUnhealthy()
        => Assert.Equal(
            HealthStatus.Unhealthy,
            ProbeOutcomes.FromException(new HttpRequestException(HttpRequestError.ConnectionError)).Status);

    [Fact]
    public void FromException_DoesNotPutTheExceptionMessageOnTheWire()
    {
        var exception = new HttpRequestException("connecting to internal-host.local:5432 failed");

        var outcome = ProbeOutcomes.FromException(exception);

        Assert.NotNull(outcome.Detail);
        Assert.DoesNotContain("internal-host.local", outcome.Detail);
    }
}

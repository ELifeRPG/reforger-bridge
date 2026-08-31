using ELifeRPG.Bridge.Api;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace ELifeRPG.Bridge.Api.UnitTests;

public sealed class ManagementSurfaceTests
{
    private const int PublicPort = 5200;
    private const int ManagementPort = 5201;

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public void IsAllowed_HealthPathsOnTheManagementPort_AreServed(string path)
        => Assert.True(ManagementSurface.IsAllowed(ManagementPort, path, ManagementPort));

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public void IsAllowed_HealthPathsOnThePublicPort_AreRejected(string path)
        => Assert.False(ManagementSurface.IsAllowed(PublicPort, path, ManagementPort));

    [Theory]
    [InlineData("/ping")]
    [InlineData("/player-connected")]
    [InlineData("/companies")]
    [InlineData("/openapi/v1.json")]
    [InlineData("/docs")]
    public void IsAllowed_ModAndDocPathsOnThePublicPort_AreServed(string path)
        => Assert.True(ManagementSurface.IsAllowed(PublicPort, path, ManagementPort));

    [Theory]
    [InlineData("/ping")]
    [InlineData("/player-connected")]
    [InlineData("/docs")]
    public void IsAllowed_ModAndDocPathsOnTheManagementPort_AreRejected(string path)
        => Assert.False(ManagementSurface.IsAllowed(ManagementPort, path, ManagementPort));

    [Fact]
    public void IsAllowed_MatchesTheHealthPrefixCaseInsensitively()
        => Assert.True(ManagementSurface.IsAllowed(ManagementPort, "/Health/Live", ManagementPort));

    [Fact]
    public void IsAllowed_DoesNotTreatAPathMerelyStartingWithHealthAsASegment()
        => Assert.True(ManagementSurface.IsAllowed(PublicPort, "/healthcheck-proxy", ManagementPort));
}

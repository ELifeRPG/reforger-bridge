using System.Text.Json;
using ELifeRPG.Bridge.Api.Endpoints;
using ELifeRPG.Bridge.Api.Serialization;
using ELifeRPG.Bridge.Api.Services;
using Xunit;

namespace ELifeRPG.Bridge.Api.UnitTests.Serialization;

/// <summary>
/// Typing the statuses must not move them on the wire. Without a JsonStringEnumConverter a .NET enum
/// serializes as a bare integer, so these assert the exact strings the mod already sees — and they
/// serialize through the very options <c>Program.cs</c> installs, which is the only way the claim is
/// worth anything.
/// </summary>
public sealed class StatusSerializationTests
{
    /// <summary>
    /// Web defaults then <see cref="BridgeJsonOptions.Configure"/>, in that order — exactly what the
    /// app gets, because ConfigureHttpJsonOptions hands out options already seeded with
    /// <see cref="JsonSerializerDefaults.Web"/> (camelCase properties) before Program.cs adds the
    /// converter. Starting from a bare JsonSerializerOptions would test a configuration that does not
    /// exist.
    /// </summary>
    private static JsonSerializerOptions Options()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        BridgeJsonOptions.Configure(options);
        return options;
    }

    [Theory]
    // Core sends these lower-cased and snake_cased (SessionDto.Create in eliferpg-core), unlike every
    // other status, so these members carry explicit JSON names.
    [InlineData(PlayerSessionStatus.Active, "\"active\"")]
    [InlineData(PlayerSessionStatus.Blocked, "\"blocked\"")]
    [InlineData(PlayerSessionStatus.NotWhitelisted, "\"not_whitelisted\"")]
    [InlineData(PlayerSessionStatus.Unlinked, "\"unlinked\"")]
    [InlineData(PlayerSessionStatus.Unknown, "\"unknown\"")]
    public void PlayerSessionStatus_SerializesToTheValueCoreUses(PlayerSessionStatus status, string expected)
        => Assert.Equal(expected, JsonSerializer.Serialize(status, Options()));

    [Theory]
    [InlineData(PhoneStatus.Active, "\"Active\"")]
    [InlineData(PhoneStatus.Suspended, "\"Suspended\"")]
    [InlineData(PhoneStatus.Deactivated, "\"Deactivated\"")]
    public void PhoneStatus_StaysPascalCase(PhoneStatus status, string expected)
        => Assert.Equal(expected, JsonSerializer.Serialize(status, Options()));

    [Theory]
    [InlineData(CompanyApplicationStatus.Pending, "\"Pending\"")]
    [InlineData(CompanyApplicationStatus.InProgress, "\"InProgress\"")]
    [InlineData(CompanyApplicationStatus.Denied, "\"Denied\"")]
    public void CompanyApplicationStatus_StaysPascalCase(CompanyApplicationStatus status, string expected)
        => Assert.Equal(expected, JsonSerializer.Serialize(status, Options()));

    [Fact]
    public void PlayerConnectedResponse_SerializesStatusAsAStringNotAnInteger()
    {
        var json = JsonSerializer.Serialize(
            new PlayerConnectedResponse(null, PlayerSessionStatus.Unlinked, false, "9EA6H5J6", null, null),
            Options());

        Assert.Contains("\"status\":\"unlinked\"", json);
        Assert.DoesNotContain("\"status\":3", json);
    }

    /// <summary>
    /// The link offer is the one part of this response the mod branches on without a status to go by,
    /// so the three fields it reads are pinned by name. Note <c>active</c> together with
    /// <c>webAccountLinked: false</c>: that combination is the normal join-first state, not an error,
    /// and a mod keying the prompt off <c>status</c> instead would never show it.
    /// </summary>
    [Fact]
    public void PlayerConnectedResponse_CarriesTheLinkOfferAnActivePlayerCanStillNeed()
    {
        var json = JsonSerializer.Serialize(
            new PlayerConnectedResponse(
                Guid.Empty,
                PlayerSessionStatus.Active,
                WebAccountLinked: false,
                LinkPin: "9EA6H5J6",
                LinkUrl: "https://id.eliferpg.de/realms/eliferpg/protocol/openid-connect/auth?link_pin=9EA6H5J6",
                Characters: []),
            Options());

        Assert.Contains("\"status\":\"active\"", json);
        Assert.Contains("\"webAccountLinked\":false", json);
        Assert.Contains("\"linkPin\":\"9EA6H5J6\"", json);
        Assert.Contains("\"linkUrl\":\"https://id.eliferpg.de", json);
    }

    /// <summary>
    /// Enfusion reads bools as 1/0, so a bool that serialized as <c>true</c>/<c>false</c> here but
    /// came back as something else would be a silent mod-side break. Pinned because
    /// <see cref="EnfusionBooleanConverter"/> sits in these very options for the request direction.
    /// </summary>
    [Fact]
    public void PlayerConnectedResponse_WritesWebAccountLinkedAsAJsonBoolean()
    {
        var json = JsonSerializer.Serialize(
            new PlayerConnectedResponse(Guid.Empty, PlayerSessionStatus.Active, true, null, null, []),
            Options());

        Assert.Contains("\"webAccountLinked\":true", json);
        Assert.DoesNotContain("\"webAccountLinked\":1", json);
    }

    [Theory]
    [InlineData(HealthStatus.Unknown, "\"unknown\"")]
    [InlineData(HealthStatus.Healthy, "\"healthy\"")]
    [InlineData(HealthStatus.Degraded, "\"degraded\"")]
    [InlineData(HealthStatus.Unhealthy, "\"unhealthy\"")]
    public void HealthStatus_SerializesToLowerCaseNames(HealthStatus status, string expected)
        => Assert.Equal(expected, JsonSerializer.Serialize(status, Options()));

    [Fact]
    public void HealthReport_SerializesTheShapeTheModReads()
    {
        var json = JsonSerializer.Serialize(
            new HealthReport(
                HealthStatus.Degraded,
                DateTimeOffset.UnixEpoch,
                [
                    new DependencyHealth(DependencyNames.Backend, HealthStatus.Healthy, 12, null),
                    new DependencyHealth(DependencyNames.Keycloak, HealthStatus.Unhealthy, 10004, "Did not answer within 10s."),
                ]),
            Options());

        Assert.Contains("\"status\":\"degraded\"", json);
        Assert.Contains("\"checkedAt\":", json);
        Assert.Contains("\"name\":\"backend\"", json);
        Assert.Contains("\"durationMs\":12", json);
        Assert.DoesNotContain("\"status\":2", json);
    }

    [Fact]
    public void HealthReport_BeforeTheFirstScan_SerializesCheckedAtAsNull()
    {
        var json = JsonSerializer.Serialize(
            new HealthReport(HealthStatus.Unknown, null, []),
            Options());

        Assert.Contains("\"checkedAt\":null", json);
        Assert.Contains("\"status\":\"unknown\"", json);
    }
}

using System.Text.Json;
using ELifeRPG.Bridge.Api.Endpoints;
using ELifeRPG.Bridge.Api.Serialization;
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
            new PlayerConnectedResponse(null, PlayerSessionStatus.Unlinked, "9EA6H5J6", null),
            Options());

        Assert.Contains("\"status\":\"unlinked\"", json);
        Assert.DoesNotContain("\"status\":3", json);
    }
}

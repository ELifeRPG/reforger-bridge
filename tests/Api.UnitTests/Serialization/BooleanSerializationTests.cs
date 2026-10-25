using System.Text.Json;
using ELifeRPG.Bridge.Api.Endpoints;
using ELifeRPG.Bridge.Api.Serialization;
using Xunit;

namespace ELifeRPG.Bridge.Api.UnitTests.Serialization;

/// <summary>
/// Enfusion packs bools as <c>1</c>/<c>0</c>; reads go through the options <c>Program.cs</c> installs.
/// </summary>
public sealed class BooleanSerializationTests
{
    private static JsonSerializerOptions Options()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        BridgeJsonOptions.Configure(options);
        return options;
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void SetPhonePowerRequest_ReadsIsPoweredOnFromNumberOrBoolean(string isPoweredOn, bool expected)
    {
        var json = $$"""{"characterId":"8a1e2d3c-4f5a-4b6c-9d7e-8f9a0b1c2d3e","isPoweredOn":{{isPoweredOn}},"pin":"4245"}""";

        var request = JsonSerializer.Deserialize<SetPhonePowerRequest>(json, Options());

        Assert.NotNull(request);
        Assert.Equal(expected, request.IsPoweredOn);
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("0", false)]
    public void UpdatePhonePreferencesRequest_ReadsDoNotDisturbFromNumberOrBoolean(string doNotDisturb, bool expected)
    {
        var json = $$"""{"characterId":"8a1e2d3c-4f5a-4b6c-9d7e-8f9a0b1c2d3e","doNotDisturb":{{doNotDisturb}}}""";

        var request = JsonSerializer.Deserialize<UpdatePhonePreferencesRequest>(json, Options());

        Assert.NotNull(request);
        Assert.Equal(expected, request.DoNotDisturb);
    }

    [Fact]
    public void UpdatePhonePreferencesRequest_WithDoNotDisturbOmitted_LeavesItNull()
    {
        // Omitted means "unchanged" — it must not read as false and switch Do Not Disturb off.
        var json = """{"characterId":"8a1e2d3c-4f5a-4b6c-9d7e-8f9a0b1c2d3e","wallpaper":"Night"}""";

        var request = JsonSerializer.Deserialize<UpdatePhonePreferencesRequest>(json, Options());

        Assert.NotNull(request);
        Assert.Null(request.DoNotDisturb);
        Assert.Equal("Night", request.Wallpaper);
    }

    [Theory]
    [InlineData("2")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("\"true\"")]
    public void Boolean_RejectsAnythingButZeroOneOrABoolean(string value)
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<bool>(value, Options()));

    [Fact]
    public void Boolean_StillWritesAJsonBoolean()
        => Assert.Equal("""{"isPoweredOn":true}""", JsonSerializer.Serialize(new PhonePowerState(true), Options()));
}

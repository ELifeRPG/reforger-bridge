using ELifeRPG.Bridge.Api.Endpoints;
using Xunit;
using ApiModels = ELifeRPG.BackendApiClient.Models;

namespace ELifeRPG.Bridge.Api.UnitTests.Endpoints;

/// <summary>
/// Core hands these across as strings. The mapping is the one place a Core-side addition breaks the
/// Bridge, so each known value is pinned and the unrecognised case is asserted to degrade rather than
/// throw — <c>unlinked</c> arrived exactly that way and a strict parse would have been a 500.
/// </summary>
public sealed class StatusMappingTests
{
    [Theory]
    [InlineData("active", PlayerSessionStatus.Active)]
    [InlineData("blocked", PlayerSessionStatus.Blocked)]
    [InlineData("not_whitelisted", PlayerSessionStatus.NotWhitelisted)]
    [InlineData("unlinked", PlayerSessionStatus.Unlinked)]
    public void PlayerSessionStatus_MapsEveryValueCoreSends(string sent, PlayerSessionStatus expected)
        => Assert.Equal(expected, PlayerSessionStatuses.FromCentralApi(sent));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Active")]              // Core sends this one lower-cased; casing is not guessed at.
    [InlineData("something_new")]
    public void PlayerSessionStatus_DegradesUnrecognisedValuesToUnknown(string? sent)
        => Assert.Equal(PlayerSessionStatus.Unknown, PlayerSessionStatuses.FromCentralApi(sent));

    [Theory]
    [InlineData("Active", PhoneStatus.Active)]
    [InlineData("Suspended", PhoneStatus.Suspended)]
    [InlineData("Deactivated", PhoneStatus.Deactivated)]
    [InlineData("something_new", PhoneStatus.Unknown)]
    [InlineData(null, PhoneStatus.Unknown)]
    public void PhoneStatus_MapsThroughPhoneSummary(string? sent, PhoneStatus expected)
    {
        var summary = PhoneSummary.Create(new ApiModels.PhoneDto
        {
            Id = Guid.NewGuid(),
            Number = "12345678",
            RegisteredTo = Guid.NewGuid(),
            Status = sent,
            IsPoweredOn = true,
        });

        Assert.Equal(expected, summary.Status);
    }

    [Theory]
    [InlineData("Pending", CompanyApplicationStatus.Pending)]
    [InlineData("InProgress", CompanyApplicationStatus.InProgress)]
    [InlineData("Accepted", CompanyApplicationStatus.Accepted)]
    [InlineData("Denied", CompanyApplicationStatus.Denied)]
    [InlineData("something_new", CompanyApplicationStatus.Unknown)]
    [InlineData(null, CompanyApplicationStatus.Unknown)]
    public void CompanyApplicationStatus_MapsThroughCompanyApplicationSummary(
        string? sent, CompanyApplicationStatus expected)
    {
        var summary = CompanyApplicationSummary.Create(new ApiModels.CompanyApplicationDto
        {
            ApplicationId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Message = "let me in",
            Status = sent,
        });

        Assert.Equal(expected, summary.Status);
    }

    [Fact]
    public void CharacterSummary_CarriesOnlyWhatCharacterSelectNeeds()
    {
        var characterId = Guid.NewGuid();

        var summary = CharacterSummary.Create(new ApiModels.CharacterDto
        {
            CharacterId = characterId,
            Name = "Tester",
            // Deliberately populated: Core still sends these, and the Bridge must drop them.
            SessionActive = true,
            SessionStartedAt = DateTimeOffset.UnixEpoch,
            SessionEndedAt = DateTimeOffset.UnixEpoch,
        });

        Assert.Equal(characterId, summary.CharacterId);
        Assert.Equal("Tester", summary.Name);

        // Asserted on the type so re-adding a session field fails here rather than quietly widening
        // the mod-facing contract again.
        Assert.DoesNotContain(
            typeof(CharacterSummary).GetProperties(),
            property => property.Name.Contains("Session", StringComparison.OrdinalIgnoreCase));
    }
}

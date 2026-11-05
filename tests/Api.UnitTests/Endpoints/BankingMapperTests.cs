using ELifeRPG.Bridge.Api.Endpoints;
using Microsoft.Kiota.Abstractions.Serialization;
using Xunit;
using ApiModels = ELifeRPG.BackendApiClient.Models;

namespace ELifeRPG.Bridge.Api.UnitTests.Endpoints;

/// <summary>
/// The banking endpoints address accounts by number and resolve it to the Central API's id. The
/// logic worth pinning is that resolution decision and the mod-facing shapes, which must carry the
/// number and never the account id.
/// </summary>
public sealed class BankingMapperTests
{
    [Fact]
    public void SingleId_ReturnsTheIdOfTheOnlyMatch()
    {
        var bankAccountId = Guid.NewGuid();

        var resolved = BankAccountLookup.SingleId([new ApiModels.BankAccountLookupDto { BankAccountId = bankAccountId }]);

        Assert.Equal(bankAccountId, resolved);
    }

    [Fact]
    public void SingleId_IsNullWhenNoAccountHoldsTheNumber()
    {
        Assert.Null(BankAccountLookup.SingleId([]));
        Assert.Null(BankAccountLookup.SingleId(null));
    }

    [Fact]
    public void SingleId_IsNullForMoreThanOneMatch()
    {
        // Can't happen (the number is unique), but must not silently pick one.
        Assert.Null(BankAccountLookup.SingleId(
        [
            new ApiModels.BankAccountLookupDto { BankAccountId = Guid.NewGuid() },
            new ApiModels.BankAccountLookupDto { BankAccountId = Guid.NewGuid() },
        ]));
    }

    [Fact]
    public void BankSummary_Create_CarriesTheBankCode()
    {
        var bankId = Guid.NewGuid();

        var summary = BankSummary.Create(new ApiModels.BankDto
        {
            BankId = bankId,
            Name = "Federal E-Life Bank",
            Code = "FEBK",
            TransactionFeeBase = new UntypedDecimal(0.20m),
            TransactionFeeMultiplier = new UntypedDecimal(0.02m),
        });

        Assert.Equal(new BankSummary(bankId, "Federal E-Life Bank", "FEBK", 0.20m, 0.02m), summary);
    }

    [Fact]
    public void BankAccountSummary_Create_MapsTheNumberAndNotTheId()
    {
        var bankId = Guid.NewGuid();
        var characterId = Guid.NewGuid();

        var summary = BankAccountSummary.Create(new ApiModels.BankAccountDto
        {
            BankAccountId = Guid.NewGuid(),
            BankId = bankId,
            Number = "EL90FEBK0123456789",
            Type = "Personal",
            CharacterId = characterId,
            CompanyId = null,
            Balance = new UntypedDecimal(12.5m),
        });

        Assert.Equal(new BankAccountSummary(bankId, "EL90FEBK0123456789", "Personal", characterId, null, 12.5m), summary);
        Assert.DoesNotContain(
            typeof(BankAccountSummary).GetProperties(),
            property => property.Name == nameof(ApiModels.BankAccountDto.BankAccountId));
    }
}

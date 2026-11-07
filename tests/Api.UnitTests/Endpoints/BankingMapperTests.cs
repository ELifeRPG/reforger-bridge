using Microsoft.AspNetCore.Http;
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

        Assert.Equal(new BankSummary(bankId, "Federal E-Life Bank", "FEBK", "0.20 Ŧ + 2 %"), summary);
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
            TransactionFeeBase = new UntypedDecimal(0.20m),
            TransactionFeeMultiplier = new UntypedDecimal(0.02m),
        });

        Assert.Equal(new BankAccountSummary(bankId, "EL90FEBK0123456789", "Personal", characterId, null, "12.50 Ŧ", "0.20 Ŧ + 2 %"), summary);
        Assert.DoesNotContain(
            typeof(BankAccountSummary).GetProperties(),
            property => property.Name == nameof(ApiModels.BankAccountDto.BankAccountId));
    }

    [Fact]
    public void BankAccountTransaction_Create_CarriesTheCounterpartyNumberNotItsId()
    {
        var transactionId = Guid.NewGuid();
        var actingCharacterId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;

        var transaction = BankAccountTransaction.Create(new ApiModels.BankAccountTransactionDto
        {
            TransactionId = transactionId,
            GameServerId = Guid.NewGuid(),
            Source = "atm-17",
            Kind = "TransferredOut",
            Amount = new UntypedDecimal(7m),
            Fee = new UntypedDecimal(0.34m),
            ActingCharacterId = actingCharacterId,
            CounterpartyBankAccountId = Guid.NewGuid(),
            CounterpartyNumber = "EL22FEBK6848015120",
            OccurredAt = occurredAt,
        });

        Assert.Equal(
            new BankAccountTransaction(transactionId, "TransferredOut", "-7.00 Ŧ", "0.34 Ŧ", actingCharacterId, "EL22FEBK6848015120", "atm-17", occurredAt),
            transaction);
        Assert.DoesNotContain(
            typeof(BankAccountTransaction).GetProperties(),
            property => property.Name == nameof(ApiModels.BankAccountTransactionDto.CounterpartyBankAccountId));
    }

    [Theory]
    [InlineData("Deposited", "+7.00 Ŧ")]
    [InlineData("TransferredIn", "+7.00 Ŧ")]
    [InlineData("Withdrawn", "-7.00 Ŧ")]
    [InlineData("TransferredOut", "-7.00 Ŧ")]
    public void BankAccountTransaction_Create_SignsTheAmountByDirection(string kind, string expected)
    {
        var transaction = BankAccountTransaction.Create(new ApiModels.BankAccountTransactionDto
        {
            TransactionId = Guid.NewGuid(),
            Kind = kind,
            Amount = new UntypedDecimal(7m),
            Fee = new UntypedDecimal(0m),
            OccurredAt = DateTimeOffset.UtcNow,
        });

        Assert.Equal(expected, transaction.AmountStr);
    }

    [Fact]
    public void TransactionResult_Create_FormatsEveryFigure()
    {
        var transactionId = Guid.NewGuid();
        var result = TransactionResult.Create(new ApiModels.TransactionResultDto
        {
            TransactionId = transactionId,
            Amount = new UntypedDecimal(1500m),
            Fee = new UntypedDecimal(30.20m),
            NewBalance = new UntypedDecimal(-25000000.5m),
        });

        Assert.Equal(new TransactionResult(transactionId, "1 500.00 Ŧ", "30.20 Ŧ", "-25 000 000.50 Ŧ"), result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidBooking_RefusesAnythingBelowOneCent(int amountCents)
    {
        var problem = Assert.IsAssignableFrom<IStatusCodeHttpResult>(BankingEndpoints.InvalidBooking(amountCents, Guid.NewGuid()));

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public void InvalidBooking_RefusesAMissingCharacter()
    {
        var problem = Assert.IsAssignableFrom<IStatusCodeHttpResult>(BankingEndpoints.InvalidBooking(1, Guid.Empty));

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public void InvalidBooking_AcceptsOneCentFromACharacter()
    {
        Assert.Null(BankingEndpoints.InvalidBooking(1, Guid.NewGuid()));
    }
}

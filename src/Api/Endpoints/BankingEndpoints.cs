using ELifeRPG.Bridge.Api.Extensions;
using ELifeRPG.BackendApiClient;
using Microsoft.Kiota.Abstractions.Serialization;
using ApiModels = ELifeRPG.BackendApiClient.Models;

namespace ELifeRPG.Bridge.Api.Endpoints;

public static class BankingEndpoints
{
    public static WebApplication MapBankingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("").WithTags("Banking");

        group.MapPost("banks", async (
                OpenBankRequest request,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                var bank = await apiClient.Api.Banks.PostAsync(
                    new ApiModels.OpenBankRequestDto
                    {
                        Name = request.Name,
                        TransactionFeeBase = new UntypedDecimal(request.TransactionFeeBase),
                        TransactionFeeMultiplier = new UntypedDecimal(request.TransactionFeeMultiplier),
                    },
                    cancellationToken: cancellationToken);

                return bank is null
                    ? Results.Problem("Central API returned an empty bank response.")
                    : Results.Ok(BankSummary.Create(bank));
            })
            .WithName("OpenBank")
            .WithDescription("Opens a new bank.");

        group.MapGet("banks", async (EliferpgApiClient apiClient, CancellationToken cancellationToken) =>
            {
                var banks = await apiClient.Api.Banks.GetAsync(cancellationToken: cancellationToken);
                return Results.Ok(banks?.Select(BankSummary.Create).ToList() ?? []);
            })
            .WithName("ListBanks")
            .WithDescription("Lists banks.");

        group.MapPost("banks/{bankId:guid}/accounts", async (
                Guid bankId,
                ApiModels.OpenBankAccountRequestDto request,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                ApiModels.BankAccountDto? account;
                try
                {
                    account = await apiClient.Api.Banks[bankId].Accounts.PostAsync(request, cancellationToken: cancellationToken);
                }
                catch (ApiModels.ProblemDetails problem)
                {
                    return Results.Problem(title: problem.Title, detail: problem.Detail, statusCode: problem.ResponseStatusCode);
                }

                return account is null
                    ? Results.Problem("Central API returned an empty bank account response.")
                    : Results.Ok(BankAccountSummary.Create(account));
            })
            .WithName("OpenBankAccount")
            .WithDescription("Opens a bank account for a character (Personal) or a company (Corporate) — provide exactly one of characterId/companyId.");

        group.MapGet("characters/{characterId:guid}/bank-accounts", async (
                Guid characterId,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                var accounts = await apiClient.Api.Characters[characterId].BankAccounts.GetAsync(cancellationToken: cancellationToken);
                return Results.Ok(accounts?.Select(BankAccountSummary.Create).ToList() ?? []);
            })
            .WithName("ListCharacterBankAccounts")
            .WithDescription("Lists a character's personal bank accounts.");

        group.MapGet("companies/{companyId:guid}/bank-accounts", async (
                Guid companyId,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                var accounts = await apiClient.Api.Companies[companyId].BankAccounts.GetAsync(cancellationToken: cancellationToken);
                return Results.Ok(accounts?.Select(BankAccountSummary.Create).ToList() ?? []);
            })
            .WithName("ListCompanyBankAccounts")
            .WithDescription("Lists a company's corporate bank accounts.");

        group.MapGet("bank-accounts/{bankAccountId:guid}", async (
                Guid bankAccountId,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                ApiModels.BankAccountDto? account;
                try
                {
                    account = await apiClient.Api.BankAccounts[bankAccountId].GetAsync(cancellationToken: cancellationToken);
                }
                catch (ApiModels.ProblemDetails problem)
                {
                    return Results.Problem(title: problem.Title, detail: problem.Detail, statusCode: problem.ResponseStatusCode);
                }

                return account is null
                    ? Results.NotFound()
                    : Results.Ok(BankAccountSummary.Create(account));
            })
            .WithName("GetBankAccount")
            .WithDescription("Gets bank account details.");

        group.MapGet("bank-accounts/{bankAccountId:guid}/transactions", async (
                Guid bankAccountId,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                List<ApiModels.BankAccountTransactionDto>? transactions;
                try
                {
                    transactions = await apiClient.Api.BankAccounts[bankAccountId].Transactions.GetAsync(cancellationToken: cancellationToken);
                }
                catch (ApiModels.ProblemDetails problem)
                {
                    return Results.Problem(title: problem.Title, detail: problem.Detail, statusCode: problem.ResponseStatusCode);
                }

                return Results.Ok(transactions?.Select(BankAccountTransaction.Create).ToList() ?? []);
            })
            .WithName("ListBankAccountTransactions")
            .WithDescription("Lists a bank account's most recent transactions (deposits, withdrawals, transfers), newest first.");

        group.MapPut("bank-accounts/{bankAccountId:guid}/deposit", async (
                Guid bankAccountId,
                DepositRequest request,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                ApiModels.TransactionResultDto? result;
                try
                {
                    result = await apiClient.Api.BankAccounts[bankAccountId].Deposit.PutAsync(
                        new ApiModels.DepositRequestDto { Amount = new UntypedDecimal(request.Amount) },
                        cancellationToken: cancellationToken);
                }
                catch (ApiModels.ProblemDetails problem)
                {
                    return Results.Problem(title: problem.Title, detail: problem.Detail, statusCode: problem.ResponseStatusCode);
                }

                return result is null
                    ? Results.Problem("Central API returned an empty deposit response.")
                    : Results.Ok(TransactionResult.Create(result));
            })
            .WithName("DepositToBankAccount")
            .WithDescription("Deposits cash into a bank account.");

        group.MapPut("bank-accounts/{bankAccountId:guid}/withdraw", async (
                Guid bankAccountId,
                WithdrawRequest request,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                ApiModels.TransactionResultDto? result;
                try
                {
                    result = await apiClient.Api.BankAccounts[bankAccountId].Withdraw.PutAsync(
                        new ApiModels.WithdrawRequestDto { Amount = new UntypedDecimal(request.Amount), CharacterId = request.CharacterId },
                        cancellationToken: cancellationToken);
                }
                catch (ApiModels.ProblemDetails problem)
                {
                    return Results.Problem(title: problem.Title, detail: problem.Detail, statusCode: problem.ResponseStatusCode);
                }

                return result is null
                    ? Results.Problem("Central API returned an empty withdraw response.")
                    : Results.Ok(TransactionResult.Create(result));
            })
            .WithName("WithdrawFromBankAccount")
            .WithDescription("Withdraws cash from a bank account, e.g. for an ATM.");

        group.MapPut("bank-accounts/{bankAccountId:guid}/transaction", async (
                Guid bankAccountId,
                TransferRequest request,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                ApiModels.TransactionResultDto? result;
                try
                {
                    result = await apiClient.Api.BankAccounts[bankAccountId].Transaction.PutAsync(
                        new ApiModels.TransferRequestDto
                        {
                            Amount = new UntypedDecimal(request.Amount),
                            CharacterId = request.CharacterId,
                            TargetBankAccountId = request.TargetBankAccountId,
                        },
                        cancellationToken: cancellationToken);
                }
                catch (ApiModels.ProblemDetails problem)
                {
                    return Results.Problem(title: problem.Title, detail: problem.Detail, statusCode: problem.ResponseStatusCode);
                }

                return result is null
                    ? Results.Problem("Central API returned an empty transfer response.")
                    : Results.Ok(TransactionResult.Create(result));
            })
            .WithName("TransferBankAccountFunds")
            .WithDescription("Transfers cash to another bank account.");

        return app;
    }
}

public sealed record BankSummary(Guid BankId, string Name, decimal TransactionFeeBase, decimal TransactionFeeMultiplier)
{
    public static BankSummary Create(ApiModels.BankDto source) => new(
        source.BankId!.Value,
        source.Name!,
        source.TransactionFeeBase!.ToDecimal(),
        source.TransactionFeeMultiplier!.ToDecimal());
}

public sealed record BankAccountSummary(
    Guid BankAccountId,
    Guid BankId,
    string Number,
    string Type,
    Guid? CharacterId,
    Guid? CompanyId,
    decimal Balance)
{
    public static BankAccountSummary Create(ApiModels.BankAccountDto source) => new(
        source.BankAccountId!.Value,
        source.BankId!.Value,
        source.Number!,
        source.Type!,
        source.CharacterId,
        source.CompanyId,
        source.Balance!.ToDecimal());
}

public sealed record OpenBankRequest(string Name, decimal TransactionFeeBase, decimal TransactionFeeMultiplier);

public sealed record BankAccountTransaction(
    string Kind,
    decimal Amount,
    decimal Fee,
    Guid? ActingCharacterId,
    Guid? CounterpartyBankAccountId,
    DateTimeOffset OccurredAt)
{
    public static BankAccountTransaction Create(ApiModels.BankAccountTransactionDto source) => new(
        source.Kind!,
        source.Amount!.ToDecimal(),
        source.Fee!.ToDecimal(),
        source.ActingCharacterId,
        source.CounterpartyBankAccountId,
        source.OccurredAt!.Value);
}

public sealed record DepositRequest(decimal Amount);

public sealed record WithdrawRequest(decimal Amount, Guid CharacterId);

public sealed record TransferRequest(decimal Amount, Guid CharacterId, Guid TargetBankAccountId);

public sealed record TransactionResult(decimal Amount, decimal Fee, decimal NewBalance)
{
    public static TransactionResult Create(ApiModels.TransactionResultDto source) => new(
        source.Amount!.ToDecimal(),
        source.Fee!.ToDecimal(),
        source.NewBalance!.ToDecimal());
}

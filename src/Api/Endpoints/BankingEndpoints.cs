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

        group.MapGet("banks", async (EliferpgApiClient apiClient, CancellationToken cancellationToken) =>
            {
                var banks = await apiClient.Api.Banks.GetAsync(cancellationToken: cancellationToken);
                return Results.Ok(banks?.Select(BankSummary.Create).ToList() ?? []);
            })
            .Produces<IEnumerable<BankSummary>>()
            .WithName("ListBanks")
            .WithDescription("Lists banks.");

        group.MapPost("banks/{bankId:guid}/accounts", async (
                Guid bankId,
                ApiModels.OpenBankAccountRequestDto request,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                return await ApiCallExtensions.ExecuteAsync(
                    () => apiClient.Api.Banks[bankId].Accounts.PostAsync(request, cancellationToken: cancellationToken),
                    account => account is null
                        ? Results.Problem("Central API returned an empty bank account response.")
                        : Results.Ok(BankAccountSummary.Create(account)));
            })
            .Produces<BankAccountSummary>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
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
            .Produces<IEnumerable<BankAccountSummary>>()
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
            .Produces<IEnumerable<BankAccountSummary>>()
            .WithName("ListCompanyBankAccounts")
            .WithDescription("Lists a company's corporate bank accounts.");

        group.MapGet("bank-accounts/{number}", async (
                string number,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                var (bankAccountId, problem) = await ResolveAsync(apiClient, number, "Bank account not found", cancellationToken);
                if (problem is not null)
                {
                    return problem;
                }

                return await ApiCallExtensions.ExecuteAsync(
                    () => apiClient.Api.BankAccounts[bankAccountId].GetAsync(cancellationToken: cancellationToken),
                    account => account is null
                        ? Results.NotFound()
                        : Results.Ok(BankAccountSummary.Create(account)));
            })
            .Produces<BankAccountSummary>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetBankAccount")
            .WithDescription("Gets bank account details.");

        group.MapGet("bank-accounts/{number}/transactions", async (
                string number,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                var (bankAccountId, problem) = await ResolveAsync(apiClient, number, "Bank account not found", cancellationToken);
                if (problem is not null)
                {
                    return problem;
                }

                return await ApiCallExtensions.ExecuteAsync(
                    () => apiClient.Api.BankAccounts[bankAccountId].Transactions.GetAsync(cancellationToken: cancellationToken),
                    transactions => Results.Ok(transactions?.Select(BankAccountTransaction.Create).ToList() ?? []));
            })
            .Produces<IEnumerable<BankAccountTransaction>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("ListBankAccountTransactions")
            .WithDescription("Lists a bank account's most recent transactions (deposits, withdrawals, transfers), newest first.");

        group.MapPut("bank-accounts/{number}/deposit", async (
                string number,
                DepositRequest request,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                var (bankAccountId, problem) = await ResolveAsync(apiClient, number, "Bank account not found", cancellationToken);
                if (problem is not null)
                {
                    return problem;
                }

                return await ApiCallExtensions.ExecuteAsync(
                    () => apiClient.Api.BankAccounts[bankAccountId].Deposit.PutAsync(
                        new ApiModels.DepositRequestDto { Amount = new UntypedDecimal(request.Amount) },
                        cancellationToken: cancellationToken),
                    result => result is null
                        ? Results.Problem("Central API returned an empty deposit response.")
                        : Results.Ok(TransactionResult.Create(result)));
            })
            .Produces<TransactionResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("DepositToBankAccount")
            .WithDescription("Deposits cash into a bank account.");

        group.MapPut("bank-accounts/{number}/withdraw", async (
                string number,
                WithdrawRequest request,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                var (bankAccountId, problem) = await ResolveAsync(apiClient, number, "Bank account not found", cancellationToken);
                if (problem is not null)
                {
                    return problem;
                }

                return await ApiCallExtensions.ExecuteAsync(
                    () => apiClient.Api.BankAccounts[bankAccountId].Withdraw.PutAsync(
                        new ApiModels.WithdrawRequestDto { Amount = new UntypedDecimal(request.Amount), CharacterId = request.CharacterId },
                        cancellationToken: cancellationToken),
                    result => result is null
                        ? Results.Problem("Central API returned an empty withdraw response.")
                        : Results.Ok(TransactionResult.Create(result)));
            })
            .Produces<TransactionResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("WithdrawFromBankAccount")
            .WithDescription("Withdraws cash from a bank account, e.g. for an ATM.");

        group.MapPut("bank-accounts/{number}/transaction", async (
                string number,
                TransferRequest request,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                var (bankAccountId, problem) = await ResolveAsync(apiClient, number, "Bank account not found", cancellationToken);
                if (problem is not null)
                {
                    return problem;
                }

                var (targetBankAccountId, targetProblem) = await ResolveAsync(
                    apiClient, request.TargetNumber, "Target bank account not found", cancellationToken);
                if (targetProblem is not null)
                {
                    return targetProblem;
                }

                return await ApiCallExtensions.ExecuteAsync(
                    () => apiClient.Api.BankAccounts[bankAccountId].Transaction.PutAsync(
                        new ApiModels.TransferRequestDto
                        {
                            Amount = new UntypedDecimal(request.Amount),
                            CharacterId = request.CharacterId,
                            TargetBankAccountId = targetBankAccountId,
                        },
                        cancellationToken: cancellationToken),
                    result => result is null
                        ? Results.Problem("Central API returned an empty transfer response.")
                        : Results.Ok(TransactionResult.Create(result)));
            })
            .Produces<TransactionResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("TransferBankAccountFunds")
            .WithDescription("Transfers cash to another bank account, given its account number as the player typed it (spaces and letter case are ignored).");

        return app;
    }

    /// <summary>
    /// The mod addresses accounts by their number; the Central API by id. Resolves one to the other
    /// through the Central API's number filter, which rejects a bad layout or bad check digits (400)
    /// before any lookup. A valid number nobody holds comes back as an empty list, answered here as
    /// a 404 with <paramref name="notFoundTitle"/> so a transfer can say which side was missing.
    /// </summary>
    private static async Task<(Guid BankAccountId, IResult? Problem)> ResolveAsync(
        EliferpgApiClient apiClient,
        string number,
        string notFoundTitle,
        CancellationToken cancellationToken)
    {
        List<ApiModels.BankAccountLookupDto>? matches;
        try
        {
            matches = await apiClient.Api.BankAccounts.GetAsync(
                configuration => configuration.QueryParameters.Number = number,
                cancellationToken);
        }
        catch (ApiModels.ProblemDetails problem)
        {
            return (Guid.Empty, Results.Problem(title: problem.Title, detail: problem.Detail, statusCode: problem.ResponseStatusCode));
        }

        return BankAccountLookup.SingleId(matches) is { } bankAccountId
            ? (bankAccountId, null)
            : (Guid.Empty, Results.Problem(title: notFoundTitle, statusCode: StatusCodes.Status404NotFound));
    }
}

public static class BankAccountLookup
{
    /// <summary>The account id from a number lookup, or null when no account holds the number.</summary>
    public static Guid? SingleId(IReadOnlyList<ApiModels.BankAccountLookupDto>? matches)
        => matches is [{ BankAccountId: { } bankAccountId }] ? bankAccountId : null;
}

/// <summary>Code is the four letters embedded in every account number the bank issues.</summary>
public sealed record BankSummary(Guid BankId, string Name, string Code, decimal TransactionFeeBase, decimal TransactionFeeMultiplier)
{
    public static BankSummary Create(ApiModels.BankDto source) => new(
        source.BankId!.Value,
        source.Name!,
        source.Code!,
        source.TransactionFeeBase!.ToDecimal(),
        source.TransactionFeeMultiplier!.ToDecimal());
}

/// <summary>
/// Carries no account id: the mod addresses accounts by Number (compact, e.g. EL90FEBK0123456789;
/// group it in fours for display), and the Bridge resolves it to the Central API's id.
/// </summary>
public sealed record BankAccountSummary(
    Guid BankId,
    string Number,
    string Type,
    Guid? CharacterId,
    Guid? CompanyId,
    decimal Balance)
{
    public static BankAccountSummary Create(ApiModels.BankAccountDto source) => new(
        source.BankId!.Value,
        source.Number!,
        source.Type!,
        source.CharacterId,
        source.CompanyId,
        source.Balance!.ToDecimal());
}

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

/// <summary>TargetNumber is the recipient's account number as the player typed it.</summary>
public sealed record TransferRequest(decimal Amount, Guid CharacterId, string TargetNumber);

public sealed record TransactionResult(decimal Amount, decimal Fee, decimal NewBalance)
{
    public static TransactionResult Create(ApiModels.TransactionResultDto source) => new(
        source.Amount!.ToDecimal(),
        source.Fee!.ToDecimal(),
        source.NewBalance!.ToDecimal());
}

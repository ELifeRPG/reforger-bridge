using ELifeRPG.Bridge.Api.Extensions;
using ELifeRPG.Bridge.Api.Formatting;
using ELifeRPG.BackendApiClient;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Kiota.Abstractions;
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
                [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
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
                        new ApiModels.DepositRequestDto
                        {
                            Amount = new UntypedDecimal(Money.FromCents(request.AmountCents)),
                            CharacterId = request.CharacterId,
                            Source = request.Source,
                        },
                        WithIdempotencyKey(idempotencyKey),
                        cancellationToken: cancellationToken),
                    result => result is null
                        ? Results.Problem("Central API returned an empty deposit response.")
                        : Results.Ok(TransactionResult.Create(result)));
            })
            .Produces<TransactionResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithName("DepositToBankAccount")
            .WithDescription("Deposits cash into a bank account.");

        group.MapPut("bank-accounts/{number}/withdraw", async (
                string number,
                WithdrawRequest request,
                [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
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
                        new ApiModels.WithdrawRequestDto
                        {
                            Amount = new UntypedDecimal(Money.FromCents(request.AmountCents)),
                            CharacterId = request.CharacterId,
                            Source = request.Source,
                        },
                        WithIdempotencyKey(idempotencyKey),
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
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithName("WithdrawFromBankAccount")
            .WithDescription("Withdraws cash from a bank account, e.g. for an ATM.");

        group.MapPut("bank-accounts/{number}/transaction", async (
                string number,
                TransferRequest request,
                [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
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
                            Amount = new UntypedDecimal(Money.FromCents(request.AmountCents)),
                            CharacterId = request.CharacterId,
                            TargetBankAccountId = targetBankAccountId,
                            Source = request.Source,
                        },
                        WithIdempotencyKey(idempotencyKey),
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
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithName("TransferBankAccountFunds")
            .WithDescription("Transfers cash to another bank account, given its account number as the player typed it (spaces and letter case are ignored).");

        return app;
    }

    public const string IdempotencyKeyHeader = "Idempotency-Key";

    /// <summary>
    /// Passes the mod's Idempotency-Key on to the Central API unchanged. The Central API checks it (400
    /// when it is missing or longer than 128 characters) and answers a resend with the first response.
    /// </summary>
    public static Action<RequestConfiguration<DefaultQueryParameters>> WithIdempotencyKey(string? idempotencyKey)
        => configuration =>
        {
            if (idempotencyKey is not null)
            {
                configuration.Headers.Add(IdempotencyKeyHeader, idempotencyKey);
            }
        };

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
            return (Guid.Empty, ApiCallExtensions.ToProblem(problem));
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
/// <summary>FeeTermsStr is the bank's current fee terms as display text (see <see cref="Money.FormatFeeTerms"/>).</summary>
public sealed record BankSummary(Guid BankId, string Name, string Code, string FeeTermsStr)
{
    public static BankSummary Create(ApiModels.BankDto source) => new(
        source.BankId!.Value,
        source.Name!,
        source.Code!,
        Money.FormatFeeTerms(source.TransactionFeeBase!.ToDecimal(), source.TransactionFeeMultiplier!.ToDecimal()));
}

/// <summary>
/// Carries no account id: the mod addresses accounts by Number (compact, e.g. EL90FEBK0123456789;
/// group it in fours for display), and the Bridge resolves it to the Central API's id. Money goes to
/// the mod as display text only (see <see cref="Money"/>), including FeeTermsStr: the fee terms copied
/// when the account was opened, which are the ones it is charged.
/// </summary>
public sealed record BankAccountSummary(
    Guid BankId,
    string Number,
    string Type,
    Guid? CharacterId,
    Guid? CompanyId,
    string BalanceStr,
    string FeeTermsStr)
{
    public static BankAccountSummary Create(ApiModels.BankAccountDto source) => new(
        source.BankId!.Value,
        source.Number!,
        source.Type!,
        source.CharacterId,
        source.CompanyId,
        Money.Format(source.Balance!.ToDecimal()),
        Money.FormatFeeTerms(source.TransactionFeeBase!.ToDecimal(), source.TransactionFeeMultiplier!.ToDecimal()));
}

/// <summary>
/// CounterpartyNumber is the other account's number for a transfer; null otherwise. AmountStr is signed
/// from this account's point of view (<c>-</c> for Withdrawn and TransferredOut, <c>+</c> otherwise);
/// Kind stays for anything else that depends on the direction, such as colour. TransactionId is shared
/// by both rows of a transfer and is what a booking response returned; Source is what the booking
/// request sent, e.g. an ATM's id.
/// </summary>
public sealed record BankAccountTransaction(
    Guid TransactionId,
    string Kind,
    string AmountStr,
    string FeeStr,
    Guid? ActingCharacterId,
    string? CounterpartyNumber,
    string? Source,
    DateTimeOffset OccurredAt)
{
    public static BankAccountTransaction Create(ApiModels.BankAccountTransactionDto source) => new(
        source.TransactionId!.Value,
        source.Kind!,
        Money.FormatSigned(source.Amount!.ToDecimal(), IsOutgoing(source.Kind!)),
        Money.Format(source.Fee!.ToDecimal()),
        source.ActingCharacterId,
        source.CounterpartyNumber,
        source.Source,
        source.OccurredAt!.Value);

    private static bool IsOutgoing(string kind) => kind is "Withdrawn" or "TransferredOut";
}

/// <summary>
/// AmountCents is in whole cents, e.g. 12345 for 123.45. CharacterId is who made the deposit; it is
/// recorded on the ledger row, not checked against the owner. Source is optional, 1 to 128 characters,
/// e.g. the ATM's id, and comes back on the ledger row.
/// </summary>
public sealed record DepositRequest(int AmountCents, Guid CharacterId, string? Source = null);

/// <summary>AmountCents is in whole cents, e.g. 12345 for 123.45. Source as on <see cref="DepositRequest"/>.</summary>
public sealed record WithdrawRequest(int AmountCents, Guid CharacterId, string? Source = null);

/// <summary>
/// AmountCents is in whole cents, e.g. 12345 for 123.45. TargetNumber is the recipient's account number
/// as the player typed it. Source as on <see cref="DepositRequest"/>.
/// </summary>
public sealed record TransferRequest(int AmountCents, Guid CharacterId, string TargetNumber, string? Source = null);

/// <summary>
/// The booking's id, which its ledger rows carry too, and its figures as display text (see
/// <see cref="Money"/>).
/// </summary>
public sealed record TransactionResult(Guid TransactionId, string AmountStr, string FeeStr, string NewBalanceStr)
{
    public static TransactionResult Create(ApiModels.TransactionResultDto source) => new(
        source.TransactionId!.Value,
        Money.Format(source.Amount!.ToDecimal()),
        Money.Format(source.Fee!.ToDecimal()),
        Money.Format(source.NewBalance!.ToDecimal()));
}

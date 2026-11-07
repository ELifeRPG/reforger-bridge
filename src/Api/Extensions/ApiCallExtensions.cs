using ApiModels = ELifeRPG.BackendApiClient.Models;

namespace ELifeRPG.Bridge.Api.Extensions;

public static class ApiCallExtensions
{
    /// <summary>
    /// Runs a Kiota-generated Central API call and maps its one consistent failure shape
    /// (ApiModels.ProblemDetails) to Results.Problem, so each endpoint only needs to describe its own
    /// success mapping. Replaces the try/catch block that used to be duplicated at every call site.
    /// </summary>
    public static async Task<IResult> ExecuteAsync<T>(Func<Task<T>> call, Func<T, IResult> onSuccess)
    {
        T result;
        try
        {
            result = await call();
        }
        catch (ApiModels.ProblemDetails problem)
        {
            return ToProblem(problem);
        }

        return onSuccess(result);
    }

    /// <summary>
    /// Passes a Central API problem on to the mod, including <c>code</c> (e.g. insufficient_balance)
    /// and <c>retryable</c> when the Central API sets them, so the mod can tell a refusal from a
    /// conflict worth resending.
    /// </summary>
    public static IResult ToProblem(ApiModels.ProblemDetails problem)
    {
        var extensions = new Dictionary<string, object?>();
        if (problem.Code is { } code)
        {
            extensions["code"] = code;
        }

        if (problem.Retryable is { } retryable)
        {
            extensions["retryable"] = retryable;
        }

        return Results.Problem(
            title: problem.Title,
            detail: problem.Detail,
            statusCode: problem.ResponseStatusCode,
            extensions: extensions);
    }
}

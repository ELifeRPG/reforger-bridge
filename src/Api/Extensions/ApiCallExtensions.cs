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
            return Results.Problem(title: problem.Title, detail: problem.Detail, statusCode: problem.ResponseStatusCode);
        }

        return onSuccess(result);
    }
}

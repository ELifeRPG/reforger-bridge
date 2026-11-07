using ELifeRPG.Bridge.Api.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Xunit;
using ApiModels = ELifeRPG.BackendApiClient.Models;

namespace ELifeRPG.Bridge.Api.UnitTests.Extensions;

public sealed class ApiCallExtensionsTests
{
    [Fact]
    public async Task ExecuteAsync_WhenCallSucceeds_ReturnsOnSuccessResult()
    {
        var result = await ApiCallExtensions.ExecuteAsync(
            () => Task.FromResult(42),
            value => Results.Ok(value));

        var ok = Assert.IsAssignableFrom<Ok<int>>(result);
        Assert.Equal(42, ok.Value);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCallThrowsProblemDetails_ReturnsResultsProblemWithSameShape()
    {
        var problem = new ApiModels.ProblemDetails
        {
            Title = "Something went wrong",
            Detail = "Details here",
            ResponseStatusCode = 409,
        };

        var result = await ApiCallExtensions.ExecuteAsync<int>(
            () => throw problem,
            value => Results.Ok(value));

        var problemResult = Assert.IsAssignableFrom<ProblemHttpResult>(result);
        Assert.Equal(409, problemResult.StatusCode);
        Assert.Equal("Something went wrong", problemResult.ProblemDetails.Title);
        Assert.Equal("Details here", problemResult.ProblemDetails.Detail);
    }

    [Fact]
    public void ToProblem_PassesOnCodeAndRetryable()
    {
        var result = ApiCallExtensions.ToProblem(new ApiModels.ProblemDetails
        {
            Title = "Another operation already committed against this account",
            ResponseStatusCode = 409,
            Code = "concurrent_modification",
            Retryable = true,
        });

        var problem = Assert.IsAssignableFrom<ProblemHttpResult>(result).ProblemDetails;
        Assert.Equal("concurrent_modification", problem.Extensions["code"]);
        Assert.Equal(true, problem.Extensions["retryable"]);
    }

    [Fact]
    public void ToProblem_LeavesThemOutWhenTheCentralApiDoesNotSetThem()
    {
        var result = ApiCallExtensions.ToProblem(new ApiModels.ProblemDetails { Title = "Bank account not found", ResponseStatusCode = 404 });

        var problem = Assert.IsAssignableFrom<ProblemHttpResult>(result).ProblemDetails;
        Assert.False(problem.Extensions.ContainsKey("code"));
        Assert.False(problem.Extensions.ContainsKey("retryable"));
    }
}


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
}

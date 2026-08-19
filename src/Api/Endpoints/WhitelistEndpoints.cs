using ELifeRPG.BackendApiClient;
using ApiModels = ELifeRPG.BackendApiClient.Models;

using ELifeRPG.Bridge.Api.Extensions;
using ELifeRPG.Bridge.Api.Services;

namespace ELifeRPG.Bridge.Api.Endpoints;

public static class WhitelistEndpoints
{
    public static WebApplication MapWhitelistEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("").WithTags("Whitelist");

        group.MapPost("submit-whitelist-application", async (
                SubmitWhitelistApplicationRequest request,
                EliferpgApiClient apiClient,
                PlayerSessionTracker sessions,
                CancellationToken cancellationToken) =>
            {
                var session = sessions.Get(request.BohemiaId);
                if (session is null)
                {
                    return Results.Problem("No active session for this Bohemia ID.", statusCode: StatusCodes.Status404NotFound);
                }

                return await ApiCallExtensions.ExecuteAsync(
                    () => apiClient.Api.WhitelistApplications.PostAsync(
                        new ApiModels.SubmitWhitelistApplicationRequestDto
                        {
                            AccountId = session.AccountId,
                            ApplicationText = request.ApplicationText,
                        },
                        cancellationToken: cancellationToken),
                    result => Results.Ok(result));
            })
            .WithName("SubmitWhitelistApplication")
            .WithDescription("Submits the connected player's whitelist application for this server.");

        return app;
    }
}

public sealed record SubmitWhitelistApplicationRequest(Guid BohemiaId, string ApplicationText);

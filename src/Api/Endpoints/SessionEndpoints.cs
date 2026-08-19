using ELifeRPG.Bridge.Api.Extensions;
using ELifeRPG.Bridge.Api.Services;
using ELifeRPG.BackendApiClient;
using ApiModels = ELifeRPG.BackendApiClient.Models;

namespace ELifeRPG.Bridge.Api.Endpoints;

public static class SessionEndpoints
{
    public static WebApplication MapSessionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("").WithTags("Session");

        group.MapPost("player-connected", async (
                PlayerConnectedRequest request,
                EliferpgApiClient apiClient,
                PlayerSessionTracker sessions,
                CancellationToken cancellationToken) =>
            {
                var session = await apiClient.Api.Accounts.SessionBootstrap.PostAsync(
                    new ApiModels.CreateSessionRequestDto { BohemiaId = request.BohemiaId },
                    cancellationToken: cancellationToken);

                if (session is null)
                {
                    return Results.Problem("Central API returned an empty session response.");
                }

                // Tracked regardless of status: PlayerSessionTracker means "this Bohemia ID is currently
                // connected and maps to this AccountId," not "is active" — a not-yet-whitelisted (or
                // blocked) player still needs a tracked session so e.g. submit-whitelist-application can
                // resolve their AccountId.
                sessions.Start(request.BohemiaId, session.AccountId!.Value);

                return Results.Ok(new PlayerConnectedResponse(session.AccountId!.Value, session.Status!));
            })
            .WithName("PlayerConnected")
            .WithDescription("Call when a player connects to the server. Resolves (or provisions) the account and starts a Bridge session for it. A blocked or not-yet-whitelisted account still gets a session (so e.g. SubmitWhitelistApplication can resolve their AccountId) — Status reports which.");

        group.MapPost("character-selected", async (
                CharacterSelectedRequest request,
                EliferpgApiClient apiClient,
                PlayerSessionTracker sessions,
                CancellationToken cancellationToken) =>
            {
                return await ApiCallExtensions.ExecuteAsync(
                    async () =>
                    {
                        await apiClient.Api.Characters[request.CharacterId].Sessions.PostAsync(cancellationToken: cancellationToken);
                        return (object?)null;
                    },
                    _ =>
                    {
                        sessions.SetActiveCharacter(request.BohemiaId, request.CharacterId);
                        return Results.Ok();
                    });
            })
            .WithName("CharacterSelected")
            .WithDescription("Call when a player picks a character at the in-game character-select screen (a separate, later moment than PlayerConnected). Starts that character's session.");

        group.MapPost("player-disconnected", async (
                PlayerDisconnectedRequest request,
                EliferpgApiClient apiClient,
                PlayerSessionTracker sessions,
                CancellationToken cancellationToken) =>
            {
                var session = sessions.End(request.BohemiaId);

                if (session?.ActiveCharacterId is { } characterId)
                {
                    return await ApiCallExtensions.ExecuteAsync(
                        async () =>
                        {
                            await apiClient.Api.Characters[characterId].Sessions.DeleteAsync(cancellationToken: cancellationToken);
                            return (object?)null;
                        },
                        _ => Results.Ok());
                }

                return Results.Ok();
            })
            .WithName("PlayerDisconnected")
            .WithDescription("Call when a player disconnects from the server. Ends the Bridge's record of the player's connection, and ends their currently-selected character's session, if one was ever selected.");

        return app;
    }
}

public sealed record PlayerConnectedRequest(Guid BohemiaId);

public sealed record PlayerConnectedResponse(Guid AccountId, string Status);

public sealed record PlayerDisconnectedRequest(Guid BohemiaId);

public sealed record CharacterSelectedRequest(Guid BohemiaId, Guid CharacterId);

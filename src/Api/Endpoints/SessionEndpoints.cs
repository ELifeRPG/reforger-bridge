using ELifeRPG.Bridge.Api.Authentication;
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
                BridgeTokenProvider tokenProvider,
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

                var playerToken = await tokenProvider.ExchangeForPlayerTokenAsync(session.KeycloakUsername!, session.Status!, cancellationToken);

                // Tracked regardless of whether a token was actually issued: PlayerSessionTracker means
                // "this Bohemia ID is currently connected and maps to this AccountId," not "has a live
                // token" — a not-yet-whitelisted (or blocked) player still needs a tracked session so
                // e.g. submit-whitelist-application can resolve their AccountId before they're ever
                // approved for a token in the first place.
                sessions.Start(
                    request.BohemiaId,
                    session.AccountId!.Value,
                    playerToken is not null && !string.IsNullOrWhiteSpace(playerToken.Jti) ? playerToken.Jti : null,
                    playerToken is not null ? DateTimeOffset.UtcNow.AddSeconds(playerToken.ExpiresInSeconds) : DateTimeOffset.UtcNow);

                if (playerToken is null)
                {
                    return Results.Ok(new PlayerConnectedResponse(session.AccountId!.Value, session.Status!, null, null));
                }

                return Results.Ok(new PlayerConnectedResponse(
                    session.AccountId!.Value,
                    session.Status!,
                    playerToken.AccessToken,
                    playerToken.ExpiresInSeconds));
            })
            .WithName("PlayerConnected")
            .WithDescription("Call when a player connects to the server. Starts a Bridge session for the account and exchanges it for a player access token. A blocked or not-yet-whitelisted account still gets a session (so e.g. SubmitWhitelistApplication can resolve their AccountId) but no token — Status reports which.");

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
                ILogger<Program> logger,
                CancellationToken cancellationToken) =>
            {
                var session = sessions.End(request.BohemiaId);

                if (session is not null && !string.IsNullOrWhiteSpace(session.Jti))
                {
                    try
                    {
                        await apiClient.Api.Accounts.Tokens.Revoke.PostAsync(
                            new ApiModels.RevokeTokenRequestDto { Jti = session.Jti, ExpiresAt = session.ExpiresAt },
                            cancellationToken: cancellationToken);
                    }
                    catch (ApiModels.ProblemDetails problem)
                    {
                        // Fire-and-forget by design: a failed revoke degrades to the pre-existing
                        // TTL-bound guarantee rather than breaking the character-session cleanup below.
                        logger.LogWarning(
                            "Failed to revoke player token for bohemiaId {BohemiaId}: {Title} - {Detail}",
                            request.BohemiaId, problem.Title, problem.Detail);
                    }
                }

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
            .WithDescription("Call when a player disconnects from the server. Ends the Bridge's record of the player's connection, revokes their player access token, and ends their currently-selected character's session, if one was ever selected.");

        return app;
    }
}

public sealed record PlayerConnectedRequest(Guid BohemiaId);

public sealed record PlayerConnectedResponse(Guid AccountId, string Status, string? PlayerAccessToken, int? ExpiresInSeconds);

public sealed record PlayerDisconnectedRequest(Guid BohemiaId);

public sealed record CharacterSelectedRequest(Guid BohemiaId, Guid CharacterId);

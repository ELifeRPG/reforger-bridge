using System.Text.Json.Serialization;
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

                // "unlinked" is the one status with no account behind it: accounts are created by web
                // signup now, not by joining, so a player Core has never seen gets a LinkPin to enter
                // on the portal instead. There is nothing to track or report an AccountId for yet.
                var status = PlayerSessionStatuses.FromCentralApi(session.Status);

                if (session.AccountId is not { } accountId)
                {
                    return Results.Ok(new PlayerConnectedResponse(null, status, session.LinkPin, null));
                }

                // Tracked regardless of status: PlayerSessionTracker means "this Bohemia ID is currently
                // connected and maps to this AccountId," not "is active". A not-yet-whitelisted (or
                // blocked) player still gets a tracked session so the mod can read their status and act
                // on it — and so any later endpoint can resolve the AccountId without re-asking Core.
                sessions.Start(request.BohemiaId, accountId);

                // Only an active account reaches the character-select screen, so only then is the list
                // worth the second call to Core — and it is the call the mod would make next anyway, so
                // this trades a round trip for nothing. Null rather than empty for the other statuses:
                // "you cannot pick a character" is a different answer from "you have none yet".
                IReadOnlyList<CharacterSummary>? characters = null;
                if (status == PlayerSessionStatus.Active)
                {
                    var loaded = await apiClient.Api.Accounts[accountId].Characters.GetAsync(cancellationToken: cancellationToken);
                    characters = loaded?.Select(CharacterSummary.Create).ToList() ?? [];
                }

                return Results.Ok(new PlayerConnectedResponse(accountId, status, session.LinkPin, characters));
            })
            .Produces<PlayerConnectedResponse>()
            .WithName("PlayerConnected")
            .WithDescription("Call when a player connects to the server. Resolves the account and starts a Bridge session for it. Status reports what the mod should do: \"active\" carries the player's characters, ready for the character-select screen; \"blocked\" and \"not_whitelisted\" carry none; \"unlinked\" means Core has never seen this Bohemia ID and carries a LinkPin for the player to enter on the web portal, with no AccountId yet.");

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
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
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
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("PlayerDisconnected")
            .WithDescription("Call when a player disconnects from the server. Ends the Bridge's record of the player's connection, and ends their currently-selected character's session, if one was ever selected.");

        return app;
    }
}

public sealed record PlayerConnectedRequest(Guid BohemiaId);

/// <summary>
/// <paramref name="AccountId"/> and <paramref name="Characters"/> are null when there is no account
/// to speak of — Status "unlinked" — and <paramref name="LinkPin"/> is set only in that case.
/// <paramref name="Characters"/> is also null for "blocked" and "not_whitelisted": those players
/// never reach character select, so the list is not fetched rather than fetched and discarded.
/// </summary>
public sealed record PlayerConnectedResponse(
    Guid? AccountId,
    PlayerSessionStatus Status,
    string? LinkPin,
    IReadOnlyList<CharacterSummary>? Characters);

/// <summary>
/// What the Central API says about a connecting player. The JSON names are pinned to the strings Core
/// emits (SessionDto.Create there), which are lower-cased and snake_cased unlike every other status on
/// this API — so typing this changed nothing on the wire.
/// </summary>
public enum PlayerSessionStatus
{
    /// <summary>
    /// Core sent a status this Bridge does not know — it has gained one since this build. Deliberately
    /// a value rather than an exception: <c>unlinked</c> was added exactly this way, and parsing
    /// strictly took player-connected down with a 500 for every unlinked player.
    /// </summary>
    [JsonStringEnumMemberName("unknown")] Unknown = 0,

    [JsonStringEnumMemberName("active")] Active,

    [JsonStringEnumMemberName("blocked")] Blocked,

    [JsonStringEnumMemberName("not_whitelisted")] NotWhitelisted,

    [JsonStringEnumMemberName("unlinked")] Unlinked,
}

public static class PlayerSessionStatuses
{
    /// <summary>
    /// Mapped explicitly rather than with Enum.TryParse: <c>not_whitelisted</c> would not parse to
    /// <c>NotWhitelisted</c> anyway, and one visible table is the right place for a contract that
    /// breaks when Core adds a value.
    /// </summary>
    public static PlayerSessionStatus FromCentralApi(string? status) => status switch
    {
        "active" => PlayerSessionStatus.Active,
        "blocked" => PlayerSessionStatus.Blocked,
        "not_whitelisted" => PlayerSessionStatus.NotWhitelisted,
        "unlinked" => PlayerSessionStatus.Unlinked,
        _ => PlayerSessionStatus.Unknown,
    };
}

public sealed record PlayerDisconnectedRequest(Guid BohemiaId);

public sealed record CharacterSelectedRequest(Guid BohemiaId, Guid CharacterId);

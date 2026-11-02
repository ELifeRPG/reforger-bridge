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
                    new ApiModels.CreateSessionRequestDto
                    {
                        GameIdentityId = request.GameIdentityId,
                        Platform = request.Platform,
                    },
                    cancellationToken: cancellationToken);

                if (session is null)
                {
                    return Results.Problem("Central API returned an empty session response.");
                }

                var status = PlayerSessionStatuses.FromCentralApi(session.Status);

                // Core always defaults WebAccountLinked, so a missing value means a response shape this
                // build does not understand. False is the safe read of that: it only offers the player a
                // link they may not need, and the prompt has nothing to show unless LinkUrl/LinkPin came
                // with it.
                var webAccountLinked = session.WebAccountLinked ?? false;

                // "unlinked" no longer means "a player Core has never seen" — joining now creates the
                // account. It means Core could not resolve the identity at all (Keycloak unreachable on a
                // first-ever join) and deliberately created nothing rather than risk a second account for
                // the same player. So there is nothing to track, and the mod should simply retry.
                if (session.AccountId is not { } accountId)
                {
                    return Results.Ok(new PlayerConnectedResponse(
                        null, status, webAccountLinked, session.LinkPin, session.LinkUrl, null));
                }

                // Tracked regardless of status: PlayerSessionTracker means "this Game Identity ID is
                // currently connected and maps to this AccountId," not "is active". A not-yet-whitelisted
                // (or blocked) player still gets a tracked session so the mod can read their status and
                // act on it — and so any later endpoint can resolve the AccountId without re-asking Core.
                sessions.Start(request.GameIdentityId, accountId);

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

                return Results.Ok(new PlayerConnectedResponse(
                    accountId, status, webAccountLinked, session.LinkPin, session.LinkUrl, characters));
            })
            .Produces<PlayerConnectedResponse>()
            .WithName("PlayerConnected")
            .WithDescription("Call when a player connects to the server. Resolves (or creates) the account and starts a Bridge session for it. Status is the play gate only: \"active\" carries the player's characters, ready for the character-select screen; \"blocked\" and \"not_whitelisted\" carry none; \"unlinked\" means Core could not resolve the identity right now and created nothing, so retry. WebAccountLinked — not Status — says whether to offer the link: an unlinked player may be perfectly active, and then LinkUrl and LinkPin carry the two ways to link.");

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
                        sessions.SetActiveCharacter(request.GameIdentityId, request.CharacterId);
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
                var session = sessions.End(request.GameIdentityId);

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

/// <param name="GameIdentityId">
/// The player's per-platform Game Identity ID, as the gameserver reports it — <b>not</b> their
/// Bohemia account, which is the web identity they may link later. One player on PC, Xbox and
/// PlayStation has three of these, and all three can end up on one account.
/// </param>
/// <param name="Platform">
/// Optional: "Pc", "Xbox" or "PlayStation". Descriptive only — Core records it on the link for
/// staff and nothing keys on it — so the mod may omit it, and Core ignores a value it does not
/// recognise rather than rejecting the join.
/// </param>
public sealed record PlayerConnectedRequest(Guid GameIdentityId, string? Platform = null);

/// <summary>
/// <paramref name="AccountId"/> and <paramref name="Characters"/> are null when there is no account
/// to speak of — Status "unlinked", which now means Core could not resolve the identity rather than
/// that the player is new. <paramref name="Characters"/> is also null for "blocked" and
/// "not_whitelisted": those players never reach character select, so the list is not fetched rather
/// than fetched and discarded.
/// </summary>
/// <param name="WebAccountLinked">
/// Whether this account already has a web identity. This, not <paramref name="Status"/>, is what
/// the mod should branch on to show the link prompt — a player who has never linked still plays
/// normally, and reads <c>active</c>.
/// </param>
/// <param name="LinkPin">
/// The 8-character PIN for a player who would rather type it into the portal. Set together with
/// <paramref name="LinkUrl"/>, and both null once linked — or when Keycloak declined to mint one,
/// in which case the player just rejoins.
/// </param>
/// <param name="LinkUrl">
/// The URL that does the whole linking flow in a browser, PIN prefilled. It is a bearer credential
/// for this game identity for as long as the PIN lives, so the mod should show it on demand (and
/// offer copy-to-clipboard) rather than leaving it on screen.
/// </param>
public sealed record PlayerConnectedResponse(
    Guid? AccountId,
    PlayerSessionStatus Status,
    bool WebAccountLinked,
    string? LinkPin,
    string? LinkUrl,
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

    /// <summary>
    /// Not "this player is new" — Core could not resolve the identity and created nothing, so the mod
    /// should retry. A genuinely new player gets an account and a playable status.
    /// </summary>
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

public sealed record PlayerDisconnectedRequest(Guid GameIdentityId);

public sealed record CharacterSelectedRequest(Guid GameIdentityId, Guid CharacterId);

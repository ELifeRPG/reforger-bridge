using System.Collections.Concurrent;

namespace ELifeRPG.Bridge.Api.Services;

public sealed record PlayerSession(Guid AccountId, string? Jti, DateTimeOffset ExpiresAt, DateTimeOffset ConnectedAt, Guid? ActiveCharacterId = null);

/// <summary>
/// Bridge-local record of which players are currently connected and which character (if any)
/// they've selected, keyed by Bohemia ID. The actual character session lifecycle is tracked in the
/// Central API (Character.SessionActive/StartSession/EndSession) — this just remembers, per
/// connected player, which character to end the session for when the player disconnects.
/// In-memory only: lost on Bridge restart. That also means a session left active in the Central API
/// by a crash/ungraceful restart isn't cleaned up here yet — StartSession there is tolerant of being
/// called again on an already-active character for exactly this reason (see its doc comment).
/// </summary>
public sealed class PlayerSessionTracker
{
    private readonly ConcurrentDictionary<Guid, PlayerSession> _sessions = new();

    public void Start(Guid bohemiaId, Guid accountId, string? jti, DateTimeOffset expiresAt)
        => _sessions[bohemiaId] = new PlayerSession(accountId, jti, expiresAt, DateTimeOffset.UtcNow);

    public PlayerSession? Get(Guid bohemiaId) => _sessions.TryGetValue(bohemiaId, out var session) ? session : null;

    public bool SetActiveCharacter(Guid bohemiaId, Guid characterId)
    {
        if (!_sessions.TryGetValue(bohemiaId, out var session))
        {
            return false;
        }

        _sessions[bohemiaId] = session with { ActiveCharacterId = characterId };
        return true;
    }

    public PlayerSession? End(Guid bohemiaId) => _sessions.TryRemove(bohemiaId, out var session) ? session : null;

    public AccountResolution ResolveAccountId(Guid bohemiaId)
    {
        var session = Get(bohemiaId);
        return session is null ? AccountResolution.NotFound() : AccountResolution.Found(session.AccountId);
    }
}

/// <summary>
/// Result of resolving a Bohemia ID to a backend AccountId via <see cref="PlayerSessionTracker.ResolveAccountId"/>.
/// Bundles a ready-to-return 404 <see cref="IResult"/> for the not-found case so callers don't have to
/// duplicate that check.
/// </summary>
public readonly record struct AccountResolution(Guid? AccountId, IResult? Error)
{
    public static AccountResolution Found(Guid accountId) => new(accountId, null);

    public static AccountResolution NotFound() => new(
        null,
        Results.Problem("No active session for this Bohemia ID.", statusCode: StatusCodes.Status404NotFound));
}

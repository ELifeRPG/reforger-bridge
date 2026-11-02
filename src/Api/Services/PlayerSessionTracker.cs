using System.Collections.Concurrent;

namespace ELifeRPG.Bridge.Api.Services;

public sealed record PlayerSession(Guid AccountId, DateTimeOffset ConnectedAt, Guid? ActiveCharacterId = null);

/// <summary>
/// Bridge-local record of which players are currently connected and which character (if any)
/// they've selected, keyed by Game Identity ID. The actual character session lifecycle is tracked
/// in the Central API (Character.SessionActive/StartSession/EndSession) — this just remembers, per
/// connected player, which character to end the session for when the player disconnects.
/// In-memory only: lost on Bridge restart. That also means a session left active in the Central API
/// by a crash/ungraceful restart isn't cleaned up here yet — StartSession there is tolerant of being
/// called again on an already-active character for exactly this reason (see its doc comment).
/// </summary>
public sealed class PlayerSessionTracker
{
    private readonly ConcurrentDictionary<Guid, PlayerSession> _sessions = new();

    public void Start(Guid gameIdentityId, Guid accountId)
        => _sessions[gameIdentityId] = new PlayerSession(accountId, DateTimeOffset.UtcNow);

    public PlayerSession? Get(Guid gameIdentityId) => _sessions.TryGetValue(gameIdentityId, out var session) ? session : null;

    public bool SetActiveCharacter(Guid gameIdentityId, Guid characterId)
    {
        if (!_sessions.TryGetValue(gameIdentityId, out var session))
        {
            return false;
        }

        _sessions[gameIdentityId] = session with { ActiveCharacterId = characterId };
        return true;
    }

    public PlayerSession? End(Guid gameIdentityId) => _sessions.TryRemove(gameIdentityId, out var session) ? session : null;

    public AccountResolution ResolveAccountId(Guid gameIdentityId)
    {
        var session = Get(gameIdentityId);
        return session is null ? AccountResolution.NotFound() : AccountResolution.Found(session.AccountId);
    }
}

/// <summary>
/// Result of resolving a Game Identity ID to a backend AccountId via <see cref="PlayerSessionTracker.ResolveAccountId"/>.
/// Bundles a ready-to-return 404 <see cref="IResult"/> for the not-found case so callers don't have to
/// duplicate that check.
/// </summary>
public readonly record struct AccountResolution(Guid? AccountId, IResult? Error)
{
    public static AccountResolution Found(Guid accountId) => new(accountId, null);

    public static AccountResolution NotFound() => new(
        null,
        Results.Problem("No active session for this Game Identity ID.", statusCode: StatusCodes.Status404NotFound));
}

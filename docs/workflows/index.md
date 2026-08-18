---
icon: lucide/workflow
---

# Workflows

Four calls from the game (via the mod) into the Bridge drive everything described here. Each
results in a call onward to the Central API, to Keycloak, or both. A fifth, Bridge-local-only
lookup (account resolution) rounds out the set.

## Player connects

`POST player-connected {bohemiaId}` — fired when a player connects to the gameserver. Starts a
Bridge session for the account and exchanges it for a player access token, unless the account is
blocked or not yet whitelisted. See [Login & Session Bootstrap](login.md) for the full flow.

## Character selected

`POST character-selected {bohemiaId, characterId}` — fired once the player picks a character at
the in-game character-select screen, a separate and later moment than connecting. Starts that
character's session in the Central API and records, Bridge-locally, which character to end the
session for on disconnect.

## Player disconnects

`POST player-disconnected {bohemiaId}` — ends the Bridge's local record of the connection, revokes
the player's access token immediately (by `jti`, rather than waiting for its ~5-minute TTL to
expire naturally), and ends whatever character session was last selected, if any, in the Central
API.

## Whitelist application submitted

`POST submit-whitelist-application {bohemiaId, applicationText}` — resolves the caller's
`AccountId` from their already-tracked Bridge session (so this works even for a not-yet-whitelisted
player, who has a session but no token) and forwards the application to the Central API.

## Resolving an account from a Bohemia ID

`GET account-info/{bohemiaId}` — looks up the `AccountId` for a currently-connected player from
the Bridge-local session, `404` if that Bohemia ID has no active session. Exists because the
Reforger server only ever knows a player's Bohemia ID, never their `AccountId`.

## Proxy endpoints

Everything else (Banking, Characters, Companies) is a thin proxy onto the Central API, using
request/response shapes local to the Bridge rather than the Central API's own Kiota-generated
types, to keep the mod-facing contract independent of client-generation details. These aren't
documented page-by-page here — see `/docs` (Scalar UI, dev-only) or `/openapi/v1.json` on a running
Bridge instance for the current, always-accurate route list.

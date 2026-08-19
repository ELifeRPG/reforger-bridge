---
icon: lucide/workflow
---

# Workflows

Four calls from the game (via the mod) into the Bridge drive everything described here. Each
results in a call onward to the Central API. The mod only ever supplies a player's Bohemia ID —
the Bridge resolves `AccountId` internally wherever it's needed (from the session established by
`player-connected`), so the mod never has to know or pass it around itself.

## Player connects

`POST player-connected {bohemiaId}` — fired when a player connects to the gameserver. Resolves (or
provisions) the account and starts a Bridge session for it, whether or not the account is blocked
or not yet whitelisted. See [Login & Session Bootstrap](login.md) for the full flow.

## Character selected

`POST character-selected {bohemiaId, characterId}` — fired once the player picks a character at
the in-game character-select screen, a separate and later moment than connecting. Starts that
character's session in the Central API and records, Bridge-locally, which character to end the
session for on disconnect.

## Player disconnects

`POST player-disconnected {bohemiaId}` — ends the Bridge's local record of the connection, and
ends whatever character session was last selected, if any, in the Central API.

## Whitelist application submitted

`POST submit-whitelist-application {bohemiaId, applicationText}` — resolves the caller's
`AccountId` from their already-tracked Bridge session (so this works even for a not-yet-whitelisted
player) and forwards the application to the Central API.

## Proxy endpoints

Everything else (Banking, Characters, Companies) is a thin proxy onto the Central API, using
request/response shapes local to the Bridge rather than the Central API's own Kiota-generated
types, to keep the mod-facing contract independent of client-generation details. Character
endpoints in particular take a `bohemiaId`, not an `AccountId` — same resolution-from-session
pattern as `submit-whitelist-application` above. These aren't documented page-by-page here — see
`/docs` (Scalar UI, dev-only) or `/openapi/v1.json` on a running Bridge instance for the current,
always-accurate route list.

---
icon: lucide/workflow
---

# Workflows

Three calls from the game (via the mod) into the Bridge drive everything described here. Each
results in a call onward to the Central API. The mod only ever supplies a player's Game Identity ID
— the Bridge resolves `AccountId` internally wherever it's needed (from the session established by
`player-connected`), so the mod never has to know or pass it around itself.

A Game Identity is per game-platform combination, so one player across PC, Xbox and PlayStation
supplies a different id from each, and all of them can be linked to a single ELifeRPG account.

## Player connects

`POST player-connected {gameIdentityId, platform?}` — fired when a player connects to the
gameserver. Resolves (or creates) the account and starts a Bridge session for it, whether or not the
account is blocked or not yet whitelisted, and answers with `webAccountLinked` plus a `linkUrl` /
`linkPin` for a player who has not linked a web account yet. See
[Login & Session Bootstrap](login.md) for the full flow.

## Character selected

`POST character-selected {gameIdentityId, characterId}` — fired once the player picks a character at
the in-game character-select screen, a separate and later moment than connecting. Starts that
character's session in the Central API and records, Bridge-locally, which character to end the
session for on disconnect.

## Player disconnects

`POST player-disconnected {gameIdentityId}` — ends the Bridge's local record of the connection, and
ends whatever character session was last selected, if any, in the Central API.

## Proxy endpoints

Everything else (Banking, Characters, Companies) is a thin proxy onto the Central API, using
request/response shapes local to the Bridge rather than the Central API's own Kiota-generated
types, to keep the mod-facing contract independent of client-generation details. Character
endpoints in particular take a `gameIdentityId`, not an `AccountId` — resolved from the session
`player-connected` established. Banking addresses accounts by **account number**, not id: routes take the compact IBAN-style
number (`bank-accounts/EL90FEBK0123456789/deposit`), and a transfer's `targetNumber` takes it as
the player typed it (spaces and letter case are ignored). The Bridge resolves each number through
the Central API's `GET bank-accounts?number=`, which rejects a bad layout or bad check digits (400)
before any lookup. A valid number nobody holds is a 404. Account summaries and transaction history
(`counterpartyNumber`) carry numbers and never ids. These aren't documented page-by-page here — see
`/docs` (Scalar UI, dev-only) or `/openapi/v1.json` on a running Bridge instance for the current,
always-accurate route list.

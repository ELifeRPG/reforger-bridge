# Design: zensical docs site for eliferpg-reforger-bridge

## Purpose

Add a standalone [zensical](https://zensical.org/)-powered documentation site to this repo,
mirroring the setup already in place in `npc-virtual-simulation` (the only sibling ELifeRPG repo
with a real zensical site today — `backend` and `backend-api-client` have plain markdown docs
folders with no site tooling). The goal is to document how the ArmA Reforger mod, the Bridge, and
`eliferpg-core`'s Central API interact — starting with the login/session-bootstrap workflow, the
first thing that happens when a player connects.

This repo's `README.md`/`AGENTS.md` already cover *running* the Bridge; this docs site is about
*how it behaves* — the request/response flows between the three components — which doesn't fit
naturally into either file.

## Scope

In scope: site tooling/infra (zensical config, build/deploy pipeline, shared brand assets) and one
fully-written workflow page (login). Out of scope, left for later follow-up work: deep-dive pages
for the other three game↔Bridge interactions (character-selected, player-disconnected,
submit-whitelist-application) and the proxy-endpoint pattern — the `workflows/index.md` overview
page will name and briefly describe each so the site's shape is visible, but only login gets full
treatment now.

## Tooling / infra

Mirrors `npc-virtual-simulation`'s setup exactly, adjusted for this repo's identity:

- **`pyproject.toml`** (new, repo root) — declares the `zensical` dependency for the docs site.
  Standalone from the .NET solution; not referenced by `global.json` or the `.slnx`.
- **`zensical.yml`** (new, repo root):
  - `site_name: eliferpg-reforger-bridge`, `site_url: https://eliferpg.github.io/reforger-bridge/`
  - `repo_url: https://github.com/ELifeRPG/reforger-bridge`, `repo_name: ELifeRPG/reforger-bridge`,
    `edit_uri: edit/main/docs/` (confirmed actual remote: `git@github.com:ELifeRPG/reforger-bridge.git`)
  - Same `theme`, `palette`, `markdown_extensions` block as `npc-virtual-simulation/zensical.yml`
    (including `pymdownx.superfences` with the `mermaid` custom fence, needed for the login
    sequence diagram)
  - `extra_css: stylesheets/extra.css`
  - `nav` matching the content structure below
- **`docs/assets/`** (new) — reuse the shared ELifeRPG brand logo/favicon SVGs, copied verbatim
  from `npc-virtual-simulation/docs/assets/` (`logo.svg`, `logo-light.svg`, `logo-icon.png`) — these
  are generic brand assets, not project-specific, per the `extra.css` comment referencing
  `ELifeRPG/docs` as the shared source of truth for the palette.
- **`docs/stylesheets/extra.css`** (new) — same brand-color override block, copied verbatim.
- **`.github/workflows/docs.yml`** (new) — identical GitHub Pages build/deploy workflow (checkout,
  setup-python, `pip install zensical`, `zensical build --clean -f zensical.yml`, upload/deploy to
  Pages). This repo already has a `.github/workflows/` directory (`ci.yml`,
  `semantic-pr-title.yml`); this file adds alongside them without touching either.

## Content structure

```
docs/
  index.md              — what the Bridge is, its role between the mod and Central API, where to start
  workflows/
    index.md            — overview: all four game <-> Bridge interactions (player-connected,
                           character-selected, player-disconnected, submit-whitelist-application)
                           plus the proxy-endpoint pattern, one short paragraph each; login links
                           out to its own page
    login.md            — deep dive on the player-connected / session-bootstrap flow
```

### `docs/index.md`

Short landing page: what the Bridge is (the gameserver-local process the mod talks to, no auth of
its own, localhost-only — from the existing `README.md`), a link into Workflows, and a link out to
`eliferpg-core`'s `ARCHITECTURE.md` for the full cross-repo system picture (same pattern as
`npc-virtual-simulation/docs/index.md` linking to the main docs site).

No `development.md` page — build/run instructions already live in `README.md`/`AGENTS.md` and
duplicating them into a second location would just create a place for them to drift out of sync.

### `docs/workflows/index.md`

Lists the four things the mod triggers on the Bridge, one short paragraph each (not full request/
response detail — that's what dedicated pages are for):

- **Player connects** (`POST player-connected`) → login.md for the full flow
- **Character selected** (`POST character-selected`) — starts that character's session in Core
- **Player disconnects** (`POST player-disconnected`) — ends the Bridge-local session, revokes the
  player's token by `jti`, ends the active character's session in Core
- **Whitelist application submitted** (`POST submit-whitelist-application`) — resolves the caller's
  `AccountId` from the already-tracked Bridge session, forwards to Core

Plus a short note on the proxy-endpoint pattern (Banking/Character/Company endpoints are thin
pass-throughs onto Core using Bridge-local DTOs, not Core's own Kiota-generated types) with a
pointer to the running instance's `/docs` (Scalar) for the current route list — not re-documented
here since it's already auto-generated and kept live by the OpenAPI pipeline.

### `docs/workflows/login.md`

The one fully-written workflow, grounded in the actual source
(`src/Api/SessionEndpoints.cs`, `src/Api/BridgeTokenProvider.cs`, `src/Api/PlayerSessionTracker.cs`,
and Core's `CreateSessionHandler`/`ResolveStatusAsync`):

1. **A mermaid sequence diagram**: Mod → Bridge (`POST player-connected {bohemiaId}`) → Core
   (`POST /api/accounts/session-bootstrap`, authenticated with the Bridge's own Client Credentials
   token) → back to Bridge with `{accountId, keycloakUsername, status}` → Bridge → Keycloak (direct
   token-exchange grant, `requested_subject=<keycloakUsername>`) → Bridge → Mod with
   `{accountId, status, playerAccessToken, expiresInSeconds}`.
2. **Why the token exchange goes Bridge → Keycloak directly, never proxied through Core**: Keycloak
   requires the client authenticating a token-exchange call to be the same client the subject token
   was issued to, so Core structurally cannot perform this exchange on the Bridge's behalf without
   holding the Bridge's own client secret (would defeat per-gameserver-instance credential
   isolation). Core's role is narrower: `session-bootstrap` only ensures the Account and its
   Keycloak user exist and reports back which Keycloak username to request. Cite
   `ARCHITECTURE.md` §4.3.
3. **Account creation on first contact**: if no Account exists for the Bohemia ID yet, Core creates
   one (`AccountStatus.Active` by default) and provisions a matching Keycloak user via the admin
   API (`KeycloakUserProvisioner.EnsureUserAsync`, no password/credentials set — the account is
   only ever reached via the Bridge's impersonation-based token exchange, never a normal Keycloak
   login).
4. **The three `Status` values and exactly what produces each** (`ResolveStatusAsync`, evaluated in
   this order):
   - `Locked` account status → always `"blocked"`, short-circuits everything else — takes
     precedence even over an approved whitelist application.
   - Else, if the calling gameserver has `WhitelistEnabled=false` → `"active"` unconditionally.
   - Else, no `Approved` `WhitelistApplication` for that `(account, gameserver)` pair →
     `"not_whitelisted"`; an approved one → `"active"`.
5. **Why `player-connected` still records a Bridge-local session even when no token is issued**: a
   blocked or not-yet-whitelisted player still needs their `AccountId` tracked
   (`PlayerSessionTracker`) so `submit-whitelist-application` can resolve it — session tracking
   means "this Bohemia ID is connected and maps to this account," not "has a live token."
6. **The disabled-Keycloak-user caveat**: locking an account disables its Keycloak user too (good
   hygiene, blocks a hypothetical normal login) but that's *not* what actually stops a blocked
   player getting a new token — Keycloak's impersonation-based token-exchange grant doesn't check
   the `enabled` flag (verified against a live instance, `ARCHITECTURE.md` §4.3). The real
   enforcement point is `BridgeTokenProvider.ExchangeForPlayerTokenAsync`'s own `status != "active"`
   check, made before it ever calls Keycloak — a structural gate on the one method that mints
   player tokens, not something every call site has to remember. An already-issued token is
   unaffected until its ~5-minute TTL naturally expires.
7. Brief mention of `player-disconnected`'s token revocation (`jti`-based, immediate, vs. the
   natural TTL) as the closing half of the session this page opens — full detail deferred to a
   future `player-disconnected` page per the overview.

## Testing / verification

Not applicable in the usual sense (no application code changes) — verification is running
`zensical build --clean -f zensical.yml` locally (or via `uv run` matching the `pyproject.toml`
convention `npc-virtual-simulation` uses) and confirming the site builds cleanly and the mermaid
diagram renders, before the GitHub Pages workflow is trusted to do the same in CI.

## Follow-up work (explicitly out of scope here)

- `workflows/character-selected.md`, `workflows/disconnected.md`,
  `workflows/whitelist-application.md` deep-dive pages.
- A `development.md` page, if/when Bridge-specific dev docs grow beyond what
  `README.md`/`AGENTS.md` comfortably hold.

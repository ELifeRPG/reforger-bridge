# ELifeRPG Bridge Docs Site Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a standalone zensical-powered documentation site to `eliferpg-reforger-bridge`, mirroring `npc-virtual-simulation`'s setup, documenting how the ArmA Reforger mod, the Bridge, and the Central API interact — starting with the login/session-bootstrap workflow.

**Architecture:** A static site built by [zensical](https://zensical.org/) from Markdown in `docs/`, configured by `zensical.yml` at the repo root, deployed to GitHub Pages via a new `.github/workflows/docs.yml` on push to `main` — entirely separate from the .NET build/solution (no project references, no shared tooling).

**Tech Stack:** zensical (Python package), [uv](https://docs.astral.sh/uv/) for dependency management, GitHub Pages/Actions for deployment.

**Spec:** `docs/superpowers/specs/2026-08-18-zensical-docs-site-design.md`

## Global Constraints

- Site tooling mirrors `npc-virtual-simulation`'s zensical setup exactly (theme, palette, `markdown_extensions`, deploy workflow) — do not introduce different config unless a task explicitly calls it out.
- `requires-python = ">=3.13"` in `pyproject.toml`, matching `npc-virtual-simulation`.
- Repo identity: `site_url: https://eliferpg.github.io/reforger-bridge/`, `repo_url: https://github.com/ELifeRPG/reforger-bridge`, `repo_name: ELifeRPG/reforger-bridge` (confirmed actual git remote: `git@github.com:ELifeRPG/reforger-bridge.git`).
- Brand assets (`logo.svg`, `logo-light.svg`, `logo-icon.png`) and `stylesheets/extra.css` are copied verbatim from `npc-virtual-simulation/docs/assets/` and `npc-virtual-simulation/docs/stylesheets/extra.css` — do not redesign them.
- Only `docs/index.md`, `docs/workflows/index.md`, and `docs/workflows/login.md` get full content in this plan; the other three workflow deep-dive pages (`character-selected`, `disconnected`, `whitelist-application`) are explicitly out of scope per the spec's "Follow-up work" section — do not create stub files for them.
- No changes to the existing .NET devcontainer, `global.json`, or build — the docs site is standalone Python/uv tooling that a contributor runs on the host, same as `npc-virtual-simulation`.

---

### Task 1: Docs site scaffolding

**Files:**
- Create: `pyproject.toml`
- Modify: `.gitignore` (append a docs-site-tooling section)
- Create: `docs/assets/logo.svg`
- Create: `docs/assets/logo-light.svg`
- Create: `docs/assets/logo-icon.png`
- Create: `docs/stylesheets/extra.css`

**Interfaces:**
- Consumes: nothing (first task)
- Produces: a `docs/` tree with `assets/` and `stylesheets/` populated, and a `pyproject.toml` that `uv sync` can install from — Task 5 (`zensical.yml`) references `docs/assets/logo.svg`, `docs/assets/logo-icon.png`, and `docs/stylesheets/extra.css` by these exact paths.

- [ ] **Step 1: Create `pyproject.toml`**

```toml
[project]
name = "eliferpg-reforger-bridge-docs"
version = "0.1.0"
requires-python = ">=3.13"
dependencies = [
    "zensical>=0.0.54",
]
```

- [ ] **Step 2: Append a docs-site-tooling section to `.gitignore`**

Add this block at the end of the existing `.gitignore` (after the `## Project tooling` section):

```
## Docs site (zensical)
.venv/
site/
.cache/
__pycache__/
```

- [ ] **Step 3: Copy the shared brand assets and stylesheet from `npc-virtual-simulation`**

```sh
mkdir -p docs/assets docs/stylesheets
cp /home/kevin/dev/projects/eliferpg/npc-virtual-simulation/docs/assets/logo.svg docs/assets/logo.svg
cp /home/kevin/dev/projects/eliferpg/npc-virtual-simulation/docs/assets/logo-light.svg docs/assets/logo-light.svg
cp /home/kevin/dev/projects/eliferpg/npc-virtual-simulation/docs/assets/logo-icon.png docs/assets/logo-icon.png
cp /home/kevin/dev/projects/eliferpg/npc-virtual-simulation/docs/stylesheets/extra.css docs/stylesheets/extra.css
```

- [ ] **Step 4: Verify `uv sync` installs zensical**

Run: `uv sync`
Expected: creates `.venv/`, resolves and installs `zensical` (and a `uv.lock` file appears), exits 0.

- [ ] **Step 5: Verify the copied files are byte-identical to their source**

Run:
```sh
diff docs/assets/logo.svg /home/kevin/dev/projects/eliferpg/npc-virtual-simulation/docs/assets/logo.svg
diff docs/assets/logo-light.svg /home/kevin/dev/projects/eliferpg/npc-virtual-simulation/docs/assets/logo-light.svg
diff docs/assets/logo-icon.png /home/kevin/dev/projects/eliferpg/npc-virtual-simulation/docs/assets/logo-icon.png
diff docs/stylesheets/extra.css /home/kevin/dev/projects/eliferpg/npc-virtual-simulation/docs/stylesheets/extra.css
```
Expected: no output from any `diff` (files identical).

- [ ] **Step 6: Commit**

```bash
git add pyproject.toml .gitignore docs/assets docs/stylesheets uv.lock
git commit -m "docs: scaffold zensical docs site tooling"
```

---

### Task 2: Home page (`docs/index.md`)

**Files:**
- Create: `docs/index.md`

**Interfaces:**
- Consumes: nothing
- Produces: `docs/index.md`, referenced by `zensical.yml`'s `nav` in Task 5 as `Home: index.md`.

- [ ] **Step 1: Write `docs/index.md`**

```markdown
---
icon: lucide/house
---

# eliferpg-reforger-bridge

The gameserver-local process the ArmA Reforger mod actually talks to — the mod never calls the
Central API (`eliferpg-core`) directly. The Bridge listens on `http://localhost:5200` with no auth
of its own; it's only ever called from the same machine, by the gameserver process itself.

This site documents how the mod, the Bridge, and the Central API actually interact at runtime —
not how to build or run the Bridge itself. For that, see the
[README](https://github.com/ELifeRPG/reforger-bridge#readme) and `AGENTS.md` in the repo.

## Where to start

- [Workflows](workflows/index.md) — the game-triggered interactions the Bridge mediates, starting
  with [Login & Session Bootstrap](workflows/login.md), the first thing that happens when a player
  connects.

For the wider cross-repo system picture — how the Bridge fits alongside the Central API, Keycloak,
and the other out-of-repo consumers — see `eliferpg-core`'s
[`ARCHITECTURE.md`](https://github.com/ELifeRPG/backend/blob/main/ARCHITECTURE.md), and
[ELifeRPG/docs](https://github.com/ELifeRPG/docs) for project-wide documentation across all
ELifeRPG repos.
```

- [ ] **Step 2: Verify required content is present and no placeholders slipped in**

Run:
```sh
grep -q "localhost:5200" docs/index.md
grep -q "Workflows" docs/index.md
grep -qi "ARCHITECTURE.md" docs/index.md
grep -qiE "TBD|TODO|FIXME" docs/index.md && echo "PLACEHOLDER FOUND" || echo "clean"
```
Expected: first three `grep -q` calls exit `0` silently; last line prints `clean`.

- [ ] **Step 3: Commit**

```bash
git add docs/index.md
git commit -m "docs: add bridge docs site home page"
```

---

### Task 3: Workflows overview (`docs/workflows/index.md`)

**Files:**
- Create: `docs/workflows/index.md`

**Interfaces:**
- Consumes: nothing
- Produces: `docs/workflows/index.md`, referenced by `zensical.yml`'s `nav` in Task 5 as the first item under the `Workflows` section, and linked to from `docs/index.md` (Task 2) and `docs/workflows/login.md` (Task 4).

- [ ] **Step 1: Write `docs/workflows/index.md`**

```markdown
---
icon: lucide/workflow
---

# Workflows

Four calls from the game (via the mod) into the Bridge drive everything described here. Each
results in a call onward to the Central API, to Keycloak, or both.

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

## Proxy endpoints

Everything else (Banking, Characters, Companies) is a thin proxy onto the Central API, using
request/response shapes local to the Bridge rather than the Central API's own Kiota-generated
types, to keep the mod-facing contract independent of client-generation details. These aren't
documented page-by-page here — see `/docs` (Scalar UI, dev-only) or `/openapi/v1.json` on a running
Bridge instance for the current, always-accurate route list.
```

- [ ] **Step 2: Verify required content is present and no placeholders slipped in**

Run:
```sh
grep -q "player-connected" docs/workflows/index.md
grep -q "character-selected" docs/workflows/index.md
grep -q "player-disconnected" docs/workflows/index.md
grep -q "submit-whitelist-application" docs/workflows/index.md
grep -q "Proxy endpoints" docs/workflows/index.md
grep -qiE "TBD|TODO|FIXME" docs/workflows/index.md && echo "PLACEHOLDER FOUND" || echo "clean"
```
Expected: all `grep -q` calls exit `0` silently; last line prints `clean`.

- [ ] **Step 3: Commit**

```bash
git add docs/workflows/index.md
git commit -m "docs: add workflows overview page"
```

---

### Task 4: Login workflow deep dive (`docs/workflows/login.md`)

**Files:**
- Create: `docs/workflows/login.md`

**Interfaces:**
- Consumes: nothing
- Produces: `docs/workflows/login.md`, referenced by `zensical.yml`'s `nav` in Task 5 as `Login & Session Bootstrap: workflows/login.md`, and the target of links from `docs/index.md` and `docs/workflows/index.md`.

- [ ] **Step 1: Write `docs/workflows/login.md`**

````markdown
---
icon: lucide/log-in
---

# Login & Session Bootstrap

`POST player-connected {bohemiaId}` is the first thing that happens when a player connects to the
gameserver — a single call from the mod's perspective, but it drives two separate token operations
across three components.

## Sequence

```mermaid
sequenceDiagram
    participant Mod as ArmA Reforger Mod
    participant Bridge
    participant Core as Central API
    participant KC as Keycloak

    Mod->>Bridge: POST player-connected {bohemiaId}
    Bridge->>Core: POST /api/accounts/session-bootstrap {bohemiaId}<br/>Bearer: Bridge's own Client Credentials token
    Note over Core: Look up Account by bohemiaId,<br/>or create Account + Keycloak user if new
    Note over Core: Resolve status: Locked always wins -> blocked;<br/>else no whitelist required -> active;<br/>else check for an approved application
    Core-->>Bridge: 200 {accountId, keycloakUsername, status}
    alt status == "active"
        Bridge->>KC: token-exchange grant<br/>subject_token = Bridge's own CC token<br/>requested_subject = keycloakUsername
        KC-->>Bridge: player access token (sub = that user's Keycloak id)
    else status == "blocked" or "not_whitelisted"
        Note over Bridge: Token exchange skipped entirely
    end
    Note over Bridge: Record Bridge-local session (PlayerSessionTracker)<br/>regardless of whether a token was issued
    Bridge-->>Mod: 200 {accountId, status, playerAccessToken, expiresInSeconds}
```

## Why the token exchange goes Bridge → Keycloak, never through Core

Keycloak requires the client authenticating a token-exchange call to be the *same* client the
subject token was issued to. Core cannot perform this exchange on the Bridge's behalf without
holding the Bridge's own client secret — which would defeat the whole point of giving each
gameserver instance its own, independently revocable OAuth client. So the two components have a
strict division of labor: Core's `session-bootstrap` only ensures the Account and its Keycloak user
exist and reports back *which* Keycloak username to request; the Bridge itself then calls
Keycloak's token endpoint directly, authenticated with its own client credentials, requesting
`requested_subject=<keycloakUsername>`. See `eliferpg-core`'s `ARCHITECTURE.md` §4.3 for the
verified mechanics behind this (tested directly against a live Keycloak instance).

## Account creation on first contact

If no `Account` exists yet for the given `bohemiaId`, Core creates one — `Active` by default — and
provisions a matching Keycloak user via Keycloak's admin API, with no password or credentials set.
That's intentional: this account is only ever reached through the Bridge's impersonation-based
token exchange, never a normal username/password Keycloak login.

## The three `status` values

Core resolves `status` in a fixed order, and returns exactly one of three strings:

| `status` | When |
|---|---|
| `blocked` | The account is `Locked` — this always wins, even over an approved whitelist application. |
| `active` | Not locked, and either the calling gameserver doesn't require whitelisting, or it does and this account has an `Approved` application for it. |
| `not_whitelisted` | Not locked, the gameserver requires whitelisting, and this account has no `Approved` application for it. |

Only an `active` status leads to a token exchange. `blocked` and `not_whitelisted` both return
`200` with `playerAccessToken: null` — never a `4xx` — so the mod always gets a well-formed
response to display to the player.

## Why a session is still tracked without a token

`player-connected` records the Bridge-local session (`PlayerSessionTracker`, in-memory, keyed by
`bohemiaId`) unconditionally — even when `status` isn't `active` and no token was issued. Session
tracking means "this Bohemia ID is currently connected and maps to this `AccountId`," not "has a
live token." That distinction matters concretely: `submit-whitelist-application` needs to resolve a
not-yet-whitelisted player's `AccountId` to file their application in the first place, and it can
only do that if a session already exists for them.

## Locking doesn't rely on Keycloak's `enabled` flag

Locking an account (`POST /api/accounts/{id}/lock`) disables its Keycloak user too, as ordinary
account hygiene. But that disable is not what actually stops a blocked player from getting a new
token — verified against a live Keycloak instance, an impersonation-based token-exchange grant for
a disabled user still succeeds. The real enforcement point is `BridgeTokenProvider`'s own
`ExchangeForPlayerTokenAsync`: it checks `status != "active"` and skips calling Keycloak entirely in
that case, structurally, as part of the one method that mints player tokens — not something every
caller has to remember to check independently. A token already issued before a lock remains valid
until it naturally expires (the realm's access-token lifespan is ~5 minutes).

## What ends the session

The counterpart to this page is disconnect: `player-disconnected` revokes the issued token
immediately by its `jti` rather than waiting out that TTL, and ends whatever character session was
last selected. See [Workflows](index.md) for a short summary of that and the other two
Bridge-mediated interactions — full treatment is planned as a follow-up page.
````

- [ ] **Step 2: Verify required content is present and no placeholders slipped in**

Run:
```sh
grep -q '```mermaid' docs/workflows/login.md
grep -q "sequenceDiagram" docs/workflows/login.md
grep -q "not_whitelisted" docs/workflows/login.md
grep -q "requested_subject" docs/workflows/login.md
grep -q "ARCHITECTURE.md" docs/workflows/login.md
grep -q "jti" docs/workflows/login.md
grep -qiE "TBD|TODO|FIXME" docs/workflows/login.md && echo "PLACEHOLDER FOUND" || echo "clean"
```
Expected: all `grep -q` calls exit `0` silently; last line prints `clean`.

- [ ] **Step 3: Commit**

```bash
git add docs/workflows/login.md
git commit -m "docs: add login and session bootstrap workflow page"
```

---

### Task 5: `zensical.yml` and a verified build

**Files:**
- Create: `zensical.yml`

**Interfaces:**
- Consumes: `docs/index.md` (Task 2), `docs/workflows/index.md` (Task 3), `docs/workflows/login.md` (Task 4), `docs/assets/logo.svg`, `docs/assets/logo-icon.png`, `docs/stylesheets/extra.css` (Task 1) — all referenced by exact path.
- Produces: a buildable site (`site/` output directory, gitignored per Task 1), consumed by the GitHub Pages workflow in Task 6.

- [ ] **Step 1: Write `zensical.yml`**

```yaml
site_url: https://eliferpg.github.io/reforger-bridge/
site_name: eliferpg-reforger-bridge
site_description: How the ArmA Reforger mod, the Bridge, and eliferpg-core's Central API interact
copyright: Copyright &copy; 2026 ELifeRPG

repo_url: https://github.com/ELifeRPG/reforger-bridge
repo_name: ELifeRPG/reforger-bridge
edit_uri: edit/main/docs/

nav:
  - Home: index.md
  - Workflows:
      - workflows/index.md
      - Login & Session Bootstrap: workflows/login.md

extra_css:
  - stylesheets/extra.css

theme:
  logo: assets/logo.svg
  favicon: assets/logo-icon.png
  language: en
  features:
    - announce.dismiss
    - content.action.edit
    - content.code.annotate
    - content.code.copy
    - content.code.select
    - content.footnote.tooltips
    - content.tabs.link
    - content.tooltips
    - navigation.footer
    - navigation.indexes
    - navigation.instant
    - navigation.instant.prefetch
    - navigation.path
    - navigation.sections
    - navigation.tabs
    - navigation.tabs.sticky
    - navigation.top
    - navigation.tracking
    - search.highlight

  font:
    text: Inter
    code: JetBrains Mono

  palette:
    - media: "(prefers-color-scheme)"
      toggle:
        icon: lucide/sun-moon
        name: Switch to light mode
    - media: "(prefers-color-scheme: light)"
      scheme: default
      primary: custom
      accent: custom
      toggle:
        icon: lucide/sun
        name: Switch to dark mode
    - media: "(prefers-color-scheme: dark)"
      scheme: slate
      primary: custom
      accent: custom
      toggle:
        icon: lucide/moon
        name: Switch to system preference

extra:
  social:
    - icon: fontawesome/brands/github
      link: https://github.com/ELifeRPG/reforger-bridge

markdown_extensions:
  - abbr
  - admonition
  - attr_list
  - def_list
  - footnotes
  - md_in_html
  - toc:
      permalink: true
  - pymdownx.arithmatex:
      generic: true
  - pymdownx.betterem
  - pymdownx.caret
  - pymdownx.details
  - pymdownx.emoji:
      emoji_generator: zensical.extensions.emoji.to_svg
      emoji_index: zensical.extensions.emoji.twemoji
  - pymdownx.highlight:
      anchor_linenums: true
      line_spans: __span
      pygments_lang_class: true
  - pymdownx.inlinehilite
  - pymdownx.keys
  - pymdownx.magiclink
  - pymdownx.mark
  - pymdownx.smartsymbols
  - pymdownx.superfences:
      custom_fences:
        - name: mermaid
          class: mermaid
          format: pymdownx.superfences.fence_code_format
  - pymdownx.tabbed:
      alternate_style: true
      combine_header_slug: true
  - pymdownx.tasklist:
      custom_checkbox: true
  - pymdownx.tilde
```

- [ ] **Step 2: Build the site and verify it succeeds**

Run: `uv run zensical build --clean -f zensical.yml`
Expected: exits `0`, creates a `site/` directory, no errors printed about missing nav targets or broken references.

- [ ] **Step 3: Verify the mermaid diagram actually rendered**

Run:
```sh
grep -rl "mermaid" site/workflows/login/index.html
```
Expected: prints the path to `site/workflows/login/index.html` (i.e., the mermaid class/script made it into the built page). If zensical inlines the diagram differently, confirm instead that `site/workflows/login/index.html` contains the literal text `sequenceDiagram` — one of the two must be true for the diagram to have survived the build.

- [ ] **Step 4: Commit**

```bash
git add zensical.yml
git commit -m "docs: add zensical site config"
```

---

### Task 6: GitHub Pages deploy workflow and README pointer

**Files:**
- Create: `.github/workflows/docs.yml`
- Modify: `README.md` (append a "Documentation site" section)

**Interfaces:**
- Consumes: `zensical.yml` (Task 5) — the workflow runs `zensical build --clean -f zensical.yml`.
- Produces: nothing consumed by a later task (final task in this plan).

- [ ] **Step 1: Write `.github/workflows/docs.yml`**

```yaml
name: Documentation
on:
  push:
    branches:
      - main
  workflow_dispatch:
permissions:
  contents: read
  pages: write
  id-token: write
concurrency:
  group: "pages"
  cancel-in-progress: false
jobs:
  deploy:
    environment:
      name: github-pages
      url: ${{ steps.deployment.outputs.page_url }}
    runs-on: ubuntu-latest
    steps:
      - uses: actions/configure-pages@v6
      - uses: actions/checkout@v7
      - uses: actions/setup-python@v6
        with:
          python-version: 3.x
      - run: pip install zensical
      - run: zensical build --clean -f zensical.yml
      - uses: actions/upload-pages-artifact@v5
        with:
          path: site
      - uses: actions/deploy-pages@v5
        id: deployment
```

- [ ] **Step 2: Verify the workflow YAML is well-formed and structurally matches the reference**

Run:
```sh
python3 -c "import yaml; yaml.safe_load(open('.github/workflows/docs.yml'))" && echo "valid yaml"
diff .github/workflows/docs.yml /home/kevin/dev/projects/eliferpg/npc-virtual-simulation/.github/workflows/docs.yml
```
Expected: `valid yaml` prints; the `diff` produces no output (the two workflow files are identical — this repo doesn't need any deploy-step differences from `npc-virtual-simulation`'s).

- [ ] **Step 3: Append a "Documentation site" section to `README.md`**

Add this section at the end of `README.md`, after the existing "## Relationship to eliferpg-core" section:

````markdown

## Documentation site

This `README`, `AGENTS.md`, and the in-repo `docs/` are the source; the rendered site is built with
[Zensical](https://zensical.org/) and deployed to GitHub Pages on push to `main` (see
`.github/workflows/docs.yml`).

Requires Python `>=3.13` and [uv](https://docs.astral.sh/uv/) — independent of the .NET devcontainer
used for the Bridge itself:

```sh
uv sync
uv run zensical serve -f zensical.yml
```

For the shared, project-wide documentation site (architecture across all ELifeRPG repos), see
[ELifeRPG/docs](https://github.com/ELifeRPG/docs).
````

- [ ] **Step 4: Verify the README section was added correctly**

Run:
```sh
grep -q "## Documentation site" README.md
grep -q "uv run zensical serve" README.md
```
Expected: both `grep -q` calls exit `0` silently.

- [ ] **Step 5: Commit**

```bash
git add .github/workflows/docs.yml README.md
git commit -m "docs: deploy docs site to GitHub Pages, point README at it"
```

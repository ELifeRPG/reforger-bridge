# eliferpg-reforger-bridge

The gameserver-local process the ArmA Reforger mod actually talks to — the mod never calls the
Central API (`eliferpg-core`) directly. Listens on `http://localhost:5200`. No auth on any of its
own endpoints — it's meant to be called only from the same machine (the gameserver process).

Talks to the Central API exclusively over HTTP, through
[`ELifeRPG.BackendApiClient`](https://github.com/ELifeRPG/backend-api-client-dotnet), a
[Kiota](https://learn.microsoft.com/openapi/kiota/)-generated C# client built from Core's OpenAPI
spec and consumed here as a NuGet package. There is no project or source dependency on
`eliferpg-core` — only a runtime HTTP dependency.

## Running

Requires `eliferpg-core`'s Central API (`src/Api`) and Keycloak running locally first (see that
repo's README). Then, from this repo:

```sh
dotnet run --project src/Api/Api.csproj
```

`src/Api/appsettings.Development.json` points `CentralApi:BaseUrl` at
`http://localhost:5100` by default.

## Connection and session lifecycle

`POST player-connected` is a local-only stand-in for the mod's connection hook (real Reforger
integration is separate, later work):

```sh
curl -X POST http://localhost:5200/player-connected \
  -H "Content-Type: application/json" \
  -d '{"bohemiaId":"11111111-1111-1111-1111-111111111111"}'
```

This exercises the full chain: Bridge → Central API (creates the account + a real Keycloak user,
if new). It also records, Bridge-locally (`PlayerSessionTracker`, in-memory, lost on Bridge
restart), that this Bohemia ID is connected and which `AccountId` it maps to — every other
mod-facing endpoint that needs an `AccountId` (character listing/creation, whitelist application)
resolves it from this tracked session rather than requiring the mod to know or pass it around.

If the account is blocked, `player-connected` still returns `200`, but with `"status": "blocked"`
and no Bridge-local session recorded.

Character sessions are a separate, later moment — `POST character-selected`
(`{"bohemiaId": "...", "characterId": "..."}`), fired once the player actually picks a character
at the in-game character-select screen, starts that character's session **in the Central API**.
`POST player-disconnected` (`{"bohemiaId": "..."}`) ends both: it clears the Bridge-local
connection record and ends whatever character session was last selected for that Bohemia ID (if
any) in the Central API.

## Proxy endpoints

The remaining endpoints are thin proxies onto the Central API, using request/response shapes
local to the Bridge (not the Central API's own DTOs) to keep the mod-facing contract independent
of Kiota's generated types. See `/docs` (Scalar UI, dev-only) or `/openapi/v1.json` on a running
instance for the current route list.

One caveat worth knowing if you touch these: several Central API response fields (bank fees,
balances, transaction amounts, member counts) come back from Kiota as its own `UntypedNode`
wrapper rather than `decimal`/`int`, since Kiota doesn't support OpenAPI's `format: double`/
`int32` cleanly. `UntypedNode.GetValue()` looks like the way to unwrap one but is hidden (not
virtually overridden) by each concrete subtype — calling it through a variable/property statically
typed as the `UntypedNode` base always throws `NotImplementedException`, regardless of the real
runtime type. `UntypedNodeExtensions.ToDecimal()`/`ToInt32()` (`src/Api/UntypedNodeExtensions.cs`)
handle this correctly — use those, not `.GetValue()` directly, in any new proxy endpoint.

## Consuming the API client

This repo no longer generates its own Kiota client. `src/Api` references the
[`ELifeRPG.BackendApiClient`](https://github.com/ELifeRPG/backend-api-client-dotnet) NuGet
package (published to GitHub Packages) instead of an in-solution `ApiClient` project — the client
is generated and versioned centrally there, against `eliferpg-core`'s OpenAPI spec.

`NuGet.config` at the repo root adds the `eliferpg-github` package source. Restoring requires a
GitHub PAT with `read:packages`, supplied via the `GITHUB_PACKAGES_USERNAME`/
`GITHUB_PACKAGES_TOKEN` environment variables (or set them as NuGet config values through
whatever secret store the devcontainer/CI environment uses).

To pick up a new client version after a breaking or additive Central API change:

```sh
dotnet add src/Api/Api.csproj package ELifeRPG.BackendApiClient --version <new-version>
```

then update the `<PackageVersion>` entry in `Directory.Packages.props` to match (central package
management requires both to agree) and review `backend-api-client-dotnet`'s release notes for
that version for any breaking changes to react to.

## Relationship to eliferpg-core

This repo used to live inside `eliferpg-core` (`src/Bridge/`). It was split out because,
code-wise, it was already fully decoupled — no project references into Core, no shared DB, HTTP
only — so keeping it in the same repo/solution gained nothing beyond a shared build. It now
follows the same pattern as `eliferpg-core`'s other out-of-repo consumers (Admin UI, NPC
Simulation Module): its own repo, own solution, consuming the Central API only through a
generated client — now via the shared `backend-api-client-dotnet` package rather than its own
Kiota generation. See `eliferpg-core`'s `ARCHITECTURE.md` §3.2/§9b/§9c for the full picture.

## Documentation site

The in-repo `docs/` directory is the source for the rendered site; it's built with
[Zensical](https://zensical.org/) and deployed to GitHub Pages on push to `main` (see
`.github/workflows/docs.yml`). This `README` and `AGENTS.md` cover build/run instructions instead
and intentionally stay off the rendered site.

Requires Python `>=3.13` and [uv](https://docs.astral.sh/uv/) — independent of the .NET devcontainer
used for the Bridge itself:

```sh
uv sync
uv run zensical serve -f zensical.yml
```

For the shared, project-wide documentation site (architecture across all ELifeRPG repos), see
[ELifeRPG/docs](https://github.com/ELifeRPG/docs).

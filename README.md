# eliferpg-reforger-bridge

The gameserver-local process the ArmA Reforger mod actually talks to — the mod never calls the
Central API (`eliferpg-core`) directly. No auth on any of its own endpoints — it's meant to be
called only from the same machine (the gameserver process).

It serves two ports, configured as Kestrel named endpoints (`Kestrel:Endpoints:Public` and
`Kestrel:Endpoints:Management`), defaulting to `8080` and `8081` and overridden to `5200` and `5201`
in Development:

| Port | Serves |
| --- | --- |
| Public (`5200` in dev) | The mod contract — `/ping` and every proxy endpoint — plus `/docs` and `/openapi/v1.json` in Development |
| Management (`5201` in dev) | `/health`, `/health/live`, `/health/ready` — nothing else |

Neither port answers the other's paths; a request for the wrong surface gets a 404. Only the public
port is meant to be exposed. Because the ports come from the Kestrel configuration section,
`ASPNETCORE_URLS` and `launchSettings.json`'s `applicationUrl` have no effect — override
`Kestrel__Endpoints__Public__Url` / `Kestrel__Endpoints__Management__Url` instead.

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

`src/Api/appsettings.Development.json` points `Backend:BaseUrl` at
`http://localhost:5100` by default.

## Connection and session lifecycle

`POST player-connected` is a local-only stand-in for the mod's connection hook (real Reforger
integration is separate, later work):

```sh
curl -X POST http://localhost:5200/player-connected \
  -H "Content-Type: application/json" \
  -d '{"gameIdentityId":"11111111-1111-1111-1111-111111111111","platform":"Pc"}'
```

`gameIdentityId` is the player's **per-platform** Game Identity ID, which is what the gameserver
reports — not their Bohemia account. One player on PC, Xbox and PlayStation has three of them, and
all three can be linked to a single ELifeRPG account. `platform` is optional and descriptive only;
Core records it on the link for staff and ignores a value it does not recognise.

This exercises the full chain: Bridge → Central API, which creates the account if this identity is
new — joining is enough to play, no web signup required. It also records, Bridge-locally
(`PlayerSessionTracker`, in-memory, lost on Bridge restart), that this Game Identity ID is connected
and which `AccountId` it maps to — every other mod-facing endpoint that needs an `AccountId`
(character listing and creation) resolves it from this tracked session rather than requiring the mod
to know or pass it around.

### Offering the web link

`player-connected` answers with `webAccountLinked`, and `linkPin` + `linkUrl` when it is `false`.
**That flag, not `status`, is what the mod branches on to offer linking** — a player who has never
touched the web portal still reads `active` and plays normally. `linkUrl` does the whole flow in a
browser with the PIN prefilled; `linkPin` is the same thing for a player who would rather type eight
characters into the portal. Both are null once linked.

`linkUrl` is a bearer credential for that game identity for as long as the PIN lives, so the mod
should show it on demand — ideally copy-to-clipboard — rather than parking it on screen.

Whitelist applications are **not** submitted through the Bridge. The Central API derives the
account from the caller's own Keycloak subject (`account:self:manage`), which a gameserver's
client-credentials token does not carry — accepting an `accountId` there previously let any holder
of the gameserver whitelist scope apply on someone else's behalf, and that was deliberately closed.
Players apply through the web portal instead, before or after ever joining the gameserver.

If the account is blocked, `player-connected` still returns `200`, but with `"status": "blocked"`.
A session *is* tracked for it, as for `not_whitelisted` — tracking means "this identity is connected
and maps to this account", not "may play". The one status with no session behind it is `unlinked`,
which no longer means "a player we have never seen": it means Core could not resolve the identity at
all (Keycloak unreachable on a first-ever join) and deliberately created nothing rather than risk a
second account for the same player, so the mod should retry.

Character sessions are a separate, later moment — `POST character-selected`
(`{"gameIdentityId": "...", "characterId": "..."}`), fired once the player actually picks a character
at the in-game character-select screen, starts that character's session **in the Central API**.
`POST player-disconnected` (`{"gameIdentityId": "..."}`) ends both: it clears the Bridge-local
connection record and ends whatever character session was last selected for that Game Identity ID
(if any) in the Central API.

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

`NuGet.config` at the repo root declares the `eliferpg-github` package source but deliberately
carries **no credentials** — they live in your user-level NuGet config instead, so that the
devcontainer (which mounts `~/.nuget` in, see `.devcontainer/compose.yml`) picks up whatever you
already have on the host. A placeholder in the repo config would sit closer in NuGet's config
hierarchy and override those mounted credentials, which is why it isn't there.

Restoring still requires a GitHub PAT with `read:packages`. Register it once, on the host:

```sh
dotnet nuget add source https://nuget.pkg.github.com/ELifeRPG/index.json \
  --name eliferpg-github --username <github-username> --password <PAT> \
  --store-password-in-clear-text --configfile ~/.nuget/NuGet/NuGet.Config
```

CI has no `~/.nuget` to mount, so it injects `secrets.GITHUB_TOKEN` into its own checked-out copy
of `NuGet.config` in a dedicated step before restore (see `.github/workflows/ci.yml`).

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

## Releasing

Binaries and images are released separately, so a .NET runtime or OS patch can ship without a new
release of this code.

1. A maintainer runs the **Release** workflow (Actions → Release → Run workflow, on `main`) and picks
   `patch`, `minor` or `major`. It builds and tests `main`, tags it `vX.Y.Z`, and attaches one
   framework-dependent publish per deployable (`reforger-bridge-<name>-X.Y.Z.tar.gz`) to a GitHub release.
2. [`ELifeRPG/reforger-bridge-container`](https://github.com/ELifeRPG/reforger-bridge-container) turns that release into
   `ghcr.io/eliferpg/reforger-bridge:latest` (and `:X.Y.Z`). Renovate opens a PR there for every new release and every
   new `aspnet` runtime image, and the image is pushed once that PR's smoke test passes and it is merged.

Every push to `main` here still builds `ghcr.io/eliferpg/reforger-bridge:edge` (and `:sha-<commit>`) from source for dev use.
`:latest` only ever carries a release.

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

## License

Copyright © 2026 ELifeRPG contributors.

Licensed under the [GNU Affero General Public License v3.0 or later](LICENSE)
(`AGPL-3.0-or-later`). You may use, change and share this software. If you
distribute it, or run a modified version for other people over a network
(for example, a game server), you must make your complete source code
available to those users under the same license.

Contributions need a DCO sign-off; see [CONTRIBUTING.md](CONTRIBUTING.md).

### Additional permission for the API description

As an additional permission under section 7 of the GNU AGPL v3, the
copyright holders of this program allow you to use, copy, modify and
distribute, under terms of your choice:

- the OpenAPI description of this program's HTTP API (the document the
  program generates and serves), and
- any code generated from that description, such as API clients
  (including the Enforce Script client in the ELifeRPG mod).

This permission covers only those two things. The rest of this program
stays under the AGPL. You may remove this permission from modified versions
you distribute.

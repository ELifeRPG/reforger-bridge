# AGENTS.md

## Building and running

This is a .NET (preview SDK, see `global.json`) solution. If no `dotnet` is available on the
host, use the devcontainer defined in `.devcontainer/` — it pins the exact SDK/runtime this repo
builds against and requires only Docker plus the `devcontainer` CLI on the host.

Bring the container up (builds the team base image via `initializeCommand`, then starts the
`workspace` service):

```sh
devcontainer up --workspace-folder .
```

Run commands inside it with `devcontainer exec`, e.g.:

```sh
devcontainer exec --workspace-folder . dotnet build src/Api/Api.csproj

devcontainer exec --workspace-folder . bash -c \
  "cd src/Api && dotnet run --no-build"
```

The container maps port 5200, so once running, `http://localhost:5200` (e.g. `/openapi/v1.json`,
`/docs` for Scalar) is reachable from the host too. `devcontainer up` is idempotent — safe to call
again if the container already exists.

The Bridge serves a second, management port (`5201` in Development) carrying `/health`,
`/health/live` and `/health/ready`; the public port 404s those paths and the management port 404s
everything else. To reach it from the host, add `5201:5201` to the `ports` list in
`.devcontainer/compose.override.local.yml` — that file is gitignored, so it is per-checkout. Note
the ports come from the Kestrel configuration section, so `ASPNETCORE_URLS` no longer has any
effect; override `Kestrel__Endpoints__Public__Url` / `Kestrel__Endpoints__Management__Url` instead.

To stop a background `dotnet run` started via `devcontainer exec`, since it isn't tied to a
foreground shell: `dotnet run`'s own process (cmdline `dotnet run --no-build`) has no
project-identifying string to `pkill -f` on, and killing only it leaves its child — the compiled
apphost, named after the assembly (`ELifeRPG.Bridge`) — still bound to the port. Kill both:

```sh
devcontainer exec --workspace-folder . bash -c "pkill -9 -f 'dotnet run'; pkill -9 -f 'ELifeRPG.Bridge'"
```

Verify the port is actually free afterward — a killed parent with a surviving child is silent
otherwise:

```sh
devcontainer exec --workspace-folder . bash -c "ss -ltnp | grep 5200"
```

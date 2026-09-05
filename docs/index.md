---
icon: lucide/house
---

# eliferpg-reforger-bridge

The gameserver-local process the ArmA Reforger mod actually talks to — the mod never calls the
Central API (`eliferpg-core`) directly. The Bridge serves the mod on `http://localhost:5200` with no
auth of its own; it's only ever called from the same machine, by the gameserver process itself. A
second, management port (`5201` in Development) carries the health and Kubernetes probe endpoints
and is not meant to be exposed.

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

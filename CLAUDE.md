# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Mexican Standoff — a fast (~10 min) realtime party game for 2–8 players (.NET 10 + SignalR backend; React frontend planned). The `docs/` folder is authoritative:

- [docs/game-design.md](docs/game-design.md) — the rules spec the engine implements. Rule changes go here first; engine code and tests follow the spec.
- [docs/tech-stack.md](docs/tech-stack.md) — architecture decisions and repo layout.
- [docs/mvp-features.md](docs/mvp-features.md) — roadmap with completion status. Engine, simulation, server, and the React frontend (`src/web`) are done; next is the drama pass (sound, avatar colors, polish) and Bicep/CI.

## Commands

.NET 10 SDK (pinned in `global.json`). Solution file is `MexicanStandoff.slnx` (XML-based .slnx format).

```powershell
dotnet build                                   # build everything
dotnet test                                    # all tests (engine unit + server integration)
dotnet test tests/MexicanStandoff.Engine.Tests # one project
dotnet test --filter "FullyQualifiedName~ChestTests.AloneOnChest_GetsGoldBar"   # single test/class
dotnet run --project src/MexicanStandoff.Server                     # hub at /hub/game, health at /health
dotnet run --project src/MexicanStandoff.Simulation -c Release -- --games 1000 --seed 12345
```

Frontend (`src/web`, React + Vite + TS): `npm run dev` starts Vite with HMR, proxying `/hub` (WebSockets) to the .NET server on port 5068 — run both for frontend work. `npm run build` type-checks and emits into the server's `wwwroot` (gitignored), so `dotnet run` alone serves the whole app; `dotnet publish` runs the SPA build automatically. Selection timer is configurable for manual testing: `Game__SelectionTimerSeconds=120 dotnet run ...`.

The simulation writes stats to stdout; conclusions are recorded in [docs/simulation-results.md](docs/simulation-results.md).

## Architecture

Three layers, dependency direction strictly inward — Engine has no dependencies, Server and Simulation both consume it:

**Engine (`src/MexicanStandoff.Engine`)** — pure, deterministic game rules. No I/O, no clock, no randomness. State is immutable records (`GameState`, `PlayerState`) updated via `with` expressions. `RoundResolver.Resolve(state, actions)` returns a `RoundResult` containing the new state plus an ordered list of `RevealStep`s. `DuelResolver` handles the 2-player Final Duel (programmed 3-action sequences, resolved step-by-step as volley rounds). All tunables live in `GameParameters` so simulation and live games share the engine. Iteration is seat-ordered for determinism — preserve that in any resolution change.

**RevealScript is the central realtime concept**: the engine emits reveal steps in phase order (actions revealed → shots → cancels → loads → chest outcomes → eliminations/loot → winner); the server broadcasts them to every device in the game group, and each device animates the same script in lockstep ("phones are the monitor" — the Monitor page is just a bigger renderer).

**Server (`src/MexicanStandoff.Server`)** — ASP.NET Core + self-hosted SignalR, in-memory state only (no database in MVP, `IGameStore` abstracts it). `GameHub` is deliberately thin: group membership plus delegation to `GameService`, which owns all orchestration (lobby, selection timers with auto-Dodge on timeout, resolution, rematch). Concurrency pattern: mutate session state only under `GameSession.Lock`, broadcast only after releasing it; selection timeouts are guarded by `SelectionNonce` so stale callbacks bail. Player identity is a secret token sent with each hub call (client keeps it in `localStorage`), so reconnects rebind to the seat. Engine types never cross the wire — `Contracts/` DTOs and views do.

**Simulation (`src/MexicanStandoff.Simulation`)** — console harness where bots (`Bots/`, one strategy each) play parameter sweeps to tune `GameParameters`. Fully seeded/reproducible: every game's RNG derives from the master seed via `HashCode.Combine`.

**Frontend (`src/web`)** — React + Vite + TS, no router/state libraries (payload matters on party wifi): a tiny path router (`router.ts`), one `useGame` hook per mounted page owning the SignalR connection and a phase state machine (connecting → joining/lobby → selecting → revealing → gameover). `types.ts` mirrors the server contracts (camelCase over the wire). Reveal playback lives in `reveal.ts` (fold `applyStep` over the pre-round snapshot, per-step durations) + `RevealStage.tsx`, shared by the phone and monitor pages. Resolved rounds are queued in `useGame` so a server timeout resolving the next round mid-playback never skips a reveal. Seat tokens: in-memory first (`tokenRef`), `localStorage` only as the cross-refresh backup.

**Tests** — engine tests are plain xUnit against the pure engine (see `TestGame.cs` for the builder). Integration tests drive full games through the real hub using `WebApplicationFactory` + SignalR client (`TestInfra.cs`); `Program` has a `public partial class Program;` line to expose the entry point — keep it.

# Tech Stack & Architecture

Guiding constraints: hobby project on existing Azure resources, minimal cost,
great local dev experience, snappy realtime gameplay.

## Decisions

| Concern | Choice | Why |
|---------|--------|-----|
| Backend | **ASP.NET Core (.NET 10 LTS) + SignalR (self-hosted)** | Preferred stack; SignalR hub gives the snappy realtime experience; self-hosted (no Azure SignalR Service) is free and fine on a single instance. .NET 10 is LTS (supported to Nov 2028); .NET 9 was STS and is already out of support. |
| Game engine | **Pure C# class library** | Deterministic, no I/O, no clock — trivially unit-testable and reusable by the simulation harness. |
| Frontend | **React + Vite + TypeScript** | Small mobile payload (players join over party wifi via QR), first-class animation ecosystem for dramatic reveals, official SignalR JS client. |
| State/storage | **In-memory, no database in MVP** | Games last ~10 min; state is tiny. Kept behind an `IGameStore` interface so Cosmos DB can slot in later for accounts/stats. |
| Infra as code | **Bicep** | One Web App on the existing App Service plan. WebSockets enabled. |
| Unit tests | **xUnit** on the engine | Exhaustive rules coverage; the engine is pure so tests are fast and deterministic. |
| Integration tests | **xUnit + WebApplicationFactory + SignalR client** | Drive full games through the real hub in-process. TestContainers joins the party when Cosmos is introduced (v2). |
| Simulation | **Console app** referencing the engine | Bots with different strategies play thousands of games across parameter grids; outputs game-length/win-rate stats. |

## Architecture

```
 Phone (React SPA) ──┐
 Phone (React SPA) ──┤  SignalR (WebSockets)   ┌──────────────────────────┐
 Phone (React SPA) ──┼────────────────────────►│ ASP.NET Core Web App     │
 Monitor (React SPA)─┘                         │  GameHub (SignalR)       │
                                               │  GameService             │
                                               │  ┌────────────────────┐  │
                                               │  │ Engine (pure C#)   │  │
                                               │  └────────────────────┘  │
                                               │  IGameStore (in-memory)  │
                                               └──────────────────────────┘
```

- **One deployable**: the Web App serves the built React SPA as static files
  and hosts the SignalR hub — no CORS, no second service, no extra cost.
- **Engine** is a pure function of `(GameState, actions) → (GameState, RevealScript)`.
  It knows nothing about SignalR, time, or players' devices.
- **RevealScript** is the key realtime concept: the engine emits an ordered
  list of reveal steps (dodges shown → shots fired → hits/HP loss → cancels →
  loads → chest outcomes → eliminations/loot → winner). The server broadcasts
  it to every device in the game; each device animates it in lockstep.
  This is how "phones are the monitor" works — the Monitor page is just a
  bigger renderer of the same script.
- **Game identity**: 4–5 letter game code (in the QR URL). Players get a
  secret player token stored in `localStorage`, so a refresh or dropped
  connection reconnects them to their seat.
- **Scale-out note**: in-memory state pins a game to one instance. Fine for a
  hobby project on a single-instance plan. If scale-out is ever needed:
  Azure SignalR Service + moving state to Cosmos/Redis.

## Repository layout

```
src/
  MexicanStandoff.Engine/          # pure game rules (no dependencies)
  MexicanStandoff.Server/          # ASP.NET Core, GameHub, serves the SPA
  MexicanStandoff.Simulation/      # bot harness for parameter tuning
  web/                             # React + Vite + TS (player + monitor pages)
tests/
  MexicanStandoff.Engine.Tests/
  MexicanStandoff.Server.IntegrationTests/
infra/
  main.bicep
docs/
```

## Local development

No Docker needed for MVP (no database):

- `dotnet run` in `MexicanStandoff.Server` (hub + API).
- `npm run dev` in `src/web` — Vite dev server proxies `/hub` and `/api`
  to the .NET server, giving HMR on the frontend.
- Multiple browser tabs (or phone + `dotnet dev-tunnels` / local IP) simulate a
  party. A `?debug` lobby mode that seeds bot players is worth adding early.

When Cosmos arrives (v2): Cosmos DB Linux emulator in Docker for local dev,
TestContainers for integration tests — as planned in the initial prompt.

## Azure deployment

- **Bicep** (`infra/main.bicep`): one Web App (Linux, .NET 10) on the existing
  App Service plan, `webSocketsEnabled: true`, HTTPS only.
- Existing Cosmos DB account: referenced but **not used** until v2.
- CI/CD: GitHub Actions — build SPA, publish .NET (SPA output into `wwwroot`),
  deploy via `azure/webapps-deploy`. Total added Azure cost: **$0** on the
  existing plan.

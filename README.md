# Mexican Standoff — A Quick Mind Game

A fast (~10 min) realtime party game for 2–8 players: unloaded guns, treasure
chests, and simultaneous action reveals. First to 6 gold bars — or last one
standing — wins.

## Running locally

```powershell
dotnet run --project src/MexicanStandoff.Server   # serves hub + SPA on :5068
```

- **Frontend work:** also run `npm run dev` in `src/web` — hot reload on :5173,
  proxying the SignalR hub to the .NET server.
- **Refresh the served SPA:** `npm run build` in `src/web` emits into the
  server's `wwwroot`; a running server picks it up immediately (no restart).
- **Publishing:** `dotnet publish` builds the SPA automatically.

Details in [docs/tech-stack.md](docs/tech-stack.md#local-development).

## Docs

- [Game design & rules spec](docs/game-design.md) — authoritative rules for the engine, tunable parameters, open questions.
- [Tech stack & architecture](docs/tech-stack.md) — .NET 10 + SignalR backend, React/Vite/TS frontend, in-memory state, Bicep on Azure.
- [MVP features & roadmap](docs/mvp-features.md) — feature checklist, v2 backlog, suggested build order.

Original brainstorm: [initial-prompt.md](initial-prompt.md).

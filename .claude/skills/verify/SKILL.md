---
name: verify
description: Build, run, and drive the Mexican Standoff app to verify a change end-to-end in the real UI.
---

# Verifying changes in the running app

## Build + launch

The SPA must be rebuilt for `dotnet run` to serve it (Vite emits into the server's `wwwroot`):

```powershell
Set-Location src\web; npm run build          # type-checks + emits into wwwroot
dotnet run --project src/MexicanStandoff.Server --launch-profile http --no-launch-profile-browser
```

Server listens on http://localhost:5068 (Development env via launch profile). For a
Production-mode run (e.g. to check dev-only gating):

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Production'; $env:ASPNETCORE_URLS = 'http://localhost:5068'
dotnet run --project src/MexicanStandoff.Server --no-launch-profile
```

Health check: GET `/health`.

## Stopping the server

`dotnet run` spawns the app as a child process named `MexicanStandoff.Server`; kill that
(not `dotnet` — that would hit unrelated builds) and the parent `dotnet run` exits with it:

```powershell
Stop-Process -Name MexicanStandoff.Server -Force -ErrorAction SilentlyContinue
```

If port 5068 is still busy afterwards (orphaned process, renamed exe), kill by port:

```powershell
Get-NetTCPConnection -LocalPort 5068 -State Listen | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force }
```

## Driving it with the browser MCP

- The browser MCP runs in Docker: use `http://host.docker.internal:5068`, not `localhost`.
- Flow: `/` → "📺 Host a game" → lands on `/monitor/CODE`; players join in another tab at `/game/CODE` (name + avatar → "Join the standoff"). First seat is the host.
- **The 30s selection timer outruns per-call MCP roundtrips** — rounds auto-resolve (absent players auto-Dodge) between snapshot calls. To act as a player, batch pick + "Lock in" in one `browser_run_code` call. Dev bots (host's "🤖 Add a bot" lobby button, Development only) keep multi-player games moving on their own.
- `browser_run_code` snippets have no `URL` global; parse `page.url()` with string ops.
- Quick state dump: `(await page.locator('#root').innerText()).slice(0, 400)`.

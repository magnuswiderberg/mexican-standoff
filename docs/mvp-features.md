# MVP Features & Roadmap

MVP philosophy: ship the core loop — join, play, dramatic reveal, win,
rematch — polished enough to be genuinely fun at a party. Everything else
waits for v2. (Decided: no healing, no asymmetric setups, no accounts/admin
in MVP.)

## MVP feature list

### 1. Game creation & joining
- [x] Create game → 4–5 letter game code + QR code (shown on whichever device created it, typically the Monitor page).
- [x] Join via QR/URL/code; pick a name and an avatar (10 character portraits with accent colors, taken ones greyed out live; the server dedupes).
- [x] Lobby: live player list on all devices; host (first player) or Monitor starts the game (2–8 players).
- [x] Reconnect: player token in `localStorage` restores your seat after refresh/drop.

### 2. Core gameplay (engine)
- [x] Full rules per [game-design.md](game-design.md): simultaneous-volley resolution, strictly-alone chests, loot-on-elimination with instant win.
- [x] Final Duel (programmed 3-action sequences) at 2 players.
- [x] Selection timer with auto-Dodge on timeout (server-side, configurable via `Game:SelectionTimerSeconds`).
- [x] All parameters injected via config (so simulation and live game share the engine) — `GameParameters`.

### 3. Player page (phone)
- [x] Action cards with target pickers (player / chest); illegal cards disabled.
- [x] Own stats always visible: HP, bullets, gold bars.
- [x] "Waiting for N players…" state after locking in.
- [x] Synchronized round reveal — every phone plays the same reveal animation in lockstep (**phones are the monitor**).
- [x] Eliminated → spectator view with reveals + standings; rematch automatically re-seats everyone.

### 4. Monitor page (optional big screen)
- [x] QR + lobby view, game state between rounds (players, HP, bullets, gold).
- [x] Big-screen rendering of the same reveal script the phones play.
- [x] Winner ceremony; rematch button.

### 5. Reveal drama
- [x] Staged reveal: cards flip → dodges → shots animate (tracer + recoil + HP flash) → hits/HP → cancels → loads → chest outcomes → eliminations/loot → win check with confetti ceremony and a monitor rematch countdown.
- [x] Sound effects — WebAudio-synthesized (no assets): flip, gunshot, dodge whoosh, load click, gold bells, standoff sting, elimination, winner fanfare, countdown ticks. Monitor defaults on, phones default muted; both have a toggle.

### 6. Rematch loop
- [x] "Play again" keeps the lobby (everyone already seated, eliminated players included), fresh game state.

### 7. Simulation harness
- [x] Console app: bots with distinct strategies (aggressive, greedy/chest-rusher, turtle, random, adaptive).
- [x] Parameter sweeps: HP, max bullets, bars-to-win, chest thresholds, duel stalemate guard.
- [x] Report: game length distribution, win rate per strategy, stalemate frequency → defaults confirmed, see [simulation-results.md](simulation-results.md).

### 8. Tests & infra
- [x] Engine unit tests (xUnit) covering every rule and edge case in the spec.
- [x] Integration tests driving full games through the SignalR hub in-process.
- [ ] Bicep for the Web App; GitHub Actions deploy.

## v2 backlog (explicitly out of MVP)

- **Healing** — pay 2 gold bars to restore 1 HP (5th action card).
- **Asymmetric setups** — varied starting loadouts (bar / loaded gun / extra HP).
- **Single-chest variant** — only ever one chest, for a more aggressive opening.
- **Accounts & saved stats** — Cosmos DB, easy re-join, stats history.
- **Admin panel** — game/statistics dashboard.
- **Bring your bot** — webhook contract so players can enter their own bot by URL.
- **Cosmos persistence** — fire-and-forget game summaries first; TestContainers-based integration tests come with it.
- **Finite chest gold pool** — if simulation shows unlimited gold drags games out.

## Suggested build order

1. **Engine + unit tests** — the rules spec above, pure C#. ✅ done
2. **Simulation harness** — validate the game is actually fun/quick on paper; lock parameters. ✅ done
3. **Server + SignalR hub + integration tests** — lobby, rounds, reveal broadcast. ✅ done
4. **Player page** — join → play → reveal loop (functional, minimal styling). ✅ done
5. **Monitor page** — reuse the reveal renderer at TV size. ✅ done
6. **Drama pass** — animations, sound, winner ceremony. ✅ done
7. **Bicep + CI/CD** — deploy, play at an actual party, iterate. ⬅ next

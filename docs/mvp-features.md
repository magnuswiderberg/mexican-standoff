# MVP Features & Roadmap

MVP philosophy: ship the core loop — join, play, dramatic reveal, win,
rematch — polished enough to be genuinely fun at a party. Everything else
waits for v2. (Decided: no healing, no asymmetric setups, no accounts/admin
in MVP.)

## MVP feature list

### 1. Game creation & joining
- [ ] Create game → 4–5 letter game code + QR code (shown on whichever device created it, typically the Monitor page).
- [ ] Join via QR/URL/code; pick a name + avatar color.
- [ ] Lobby: live player list on all devices; host starts the game (2–8 players).
- [ ] Reconnect: player token in `localStorage` restores your seat after refresh/drop.

### 2. Core gameplay (engine)
- [x] Full rules per [game-design.md](game-design.md): simultaneous-volley resolution, strictly-alone chests, loot-on-elimination with instant win.
- [x] Final Duel (programmed 3-action sequences) at 2 players.
- [ ] Selection timer with auto-Dodge on timeout. *(server concern — the engine is clock-free)*
- [x] All parameters injected via config (so simulation and live game share the engine) — `GameParameters`.

### 3. Player page (phone)
- [ ] Action cards with target pickers (player / chest); illegal cards disabled.
- [ ] Own stats always visible: HP, bullets, gold bars.
- [ ] "Waiting for N players…" state after locking in.
- [ ] Synchronized round reveal — every phone plays the same reveal animation in lockstep (**phones are the monitor**).
- [ ] Eliminated → spectator view with reveals + standings, and one-tap join when a rematch starts.

### 4. Monitor page (optional big screen)
- [ ] QR + lobby view, game state between rounds (players, HP, bullets, gold).
- [ ] Big-screen rendering of the same reveal script the phones play.
- [ ] Winner ceremony; rematch button.

### 5. Reveal drama
- [ ] Staged reveal: cards flip → dodges → shots animate → hits/HP → cancels → loads → chest outcomes → eliminations/loot → win check.
- [ ] Sound effects (phones can be muted; the Monitor carries the room).

### 6. Rematch loop
- [ ] "Play again" keeps the lobby (everyone already seated, eliminated players included), fresh game state.

### 7. Simulation harness
- [x] Console app: bots with distinct strategies (aggressive, greedy/chest-rusher, turtle, random, adaptive).
- [x] Parameter sweeps: HP, max bullets, bars-to-win, chest thresholds, duel stalemate guard.
- [x] Report: game length distribution, win rate per strategy, stalemate frequency → defaults confirmed, see [simulation-results.md](simulation-results.md).

### 8. Tests & infra
- [x] Engine unit tests (xUnit) covering every rule and edge case in the spec.
- [ ] Integration tests driving full games through the SignalR hub in-process.
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
3. **Server + SignalR hub + integration tests** — lobby, rounds, reveal broadcast. ⬅ next
4. **Player page** — join → play → reveal loop (functional, minimal styling).
5. **Monitor page** — reuse the reveal renderer at TV size.
6. **Drama pass** — animations, sound, winner ceremony.
7. **Bicep + CI/CD** — deploy, play at an actual party, iterate.

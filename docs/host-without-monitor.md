# Hosting without a monitor

A group with phones and no TV should be able to start a game. Today they can't —
not without stumbling into it. This doc records what's broken and what we're
changing.

## Where we are

The monitor-less mode already *works*; it just has no front door.

- The only way to create a game is **"📺 Host a game on this screen"**
  (`HomePage.hostGame`). It calls `CreateGame`, stores the monitor token, and
  navigates to `/monitor/CODE`. Creating a game and becoming the monitor are the
  same act.
- Phone-hosting is fully implemented on both sides: the first player to join is
  the host (`PlayerPage`: `lobby.players[0].id === playerId`) and gets Start,
  kick, add-bot and rematch; the server accepts the host's seat token as a
  control token (`GameService.RequireController`). Rematch falls to the host
  whenever no monitor page is connected (`hasMonitor`).
- The only path into it is an accident: create the game, land on the monitor,
  tap the QR — which is an `<a href={joinUrl}>` that navigates *the same tab* to
  `/game/CODE`. The monitor vanishes, you become player #1, and the game plays
  fine.

### The three problems

1. **No intuitive entry.** Nothing on the home page says "start a game and play
   on this phone". The one create button is marked 📺 and reads as *this device
   is the TV*.
2. **The QR is monitor-only.** `react-qr-code` is imported in `MonitorPage` and
   nowhere else. The phone lobby shows the code as a text header and offers no
   QR, no link, no share, no copy. A phone-only host has to read the code aloud
   and everyone types it — the worst of the three ways into a game.
3. **The monitor QR is a footgun.** Tapping it on the hosting device silently
   destroys the monitor (same-tab navigation). It's the only affordance that
   *looks* like "I want to play too", and it works by side effect.

## What we're doing

### 1. Two named ways to host

Split the home page by device, and drop the 📺 from the phone one.

- **🤠 Host & play** — `CreateGame`, save the monitor token anyway, then navigate
  to `/game/CODE`. The join form appears, you pick name + avatar, you're player
  #1 and therefore host. No server change: no monitor page ever connects, so
  `hasMonitor` stays false and rematch correctly stays on the phone.
- **📺 Host on a big screen** — today's behaviour, unchanged.

### 2. An invite panel in the phone lobby

Big code + QR + **Share link** (`navigator.share`, clipboard fallback). Others
scan the host's phone screen. Expanded by default while the host is alone;
collapsible once players arrive. `react-qr-code` is already a dependency, so this
costs nothing in bundle size.

### 3. Defuse the monitor QR

Either `target="_blank"` on the QR link, or replace the implicit link with an
explicit **"Also play on this device"** button that opens `/game/CODE` in a new
tab. Clicking the QR must never leave the room without a monitor by surprise.

## 4. The phone → TV handoff

The monitor token is minted by `CreateGame` and only ever lives in the creating
device's `localStorage` (`session.ts: saveMonitorToken`). A game started on a phone
therefore had no way to gain a TV later: the big screen cannot prove it is the
board, and the token cannot be shouted across the room.

A QR is the wrong shape here — TVs and laptops don't have cameras. So the screen
**asks, and the host allows**:

0. The TV gets to `/monitor/CODE` one of two ways: the host taps **"📺 Share board
   link"** in the invite panel and the laptop opens it with nothing to type, or
   someone types the site and the game code on the TV itself and taps **"📺 Show
   that game's board on this screen"**. The invite panel spells out both.
1. Either way it lands on `/monitor/CODE`.
2. The monitor page finds no token, so instead of the old dead-end error it calls
   `RequestMonitor` and displays a 4-character **pair code**.
3. Every device in the game hears `MonitorRequested` — it carries no authority, so
   broadcasting it is safe — but only the **host's** phone renders a prompt:
   *"A screen wants to show the board. Does it say DC4Y?"*
4. The host taps yes. `DecideMonitor` takes a control token (host seat or monitor),
   and the server sends `MonitorDecision` **to the asking connection alone** — that
   is the only message that ever carries the monitor token.
5. The TV stores it and becomes the board. Rematches move to it, as in any monitor
   game.

Why a pair code at all: anyone who knows the (public) game code may *ask*, so the
host needs to know they are approving the TV in front of them and not a stranger's
browser. Asking grants nothing on its own. One request is pending at a time — a
second asker replaces the first, so prompts cannot be stacked up on the host — and
a request goes stale after two minutes.

### Between games only

A screen can only ask while the game is in the lobby or on the win screen. Mid-round
the prompt would land on the host's phone on top of the action picker — a mis-tap
waiting to happen — and nobody actually sets up a TV during a duel. A screen that
turns up mid-game is told *"a board goes up between games — ask again when this one
ends"*, with an **Ask again** button; the room plays the game out (or stops it) and
the board goes up for the next one. If the host starts the game while a screen is
still waiting, that screen is told so rather than being left on "waiting for the
host" for a whole game.

**The shared board link carries no token** — deliberately. Putting the monitor token
in the URL would skip the pairing step, but it would also park the room's control
token (start, stop, kick) in a chat log that anyone can forward. The link is a
shortcut to the pairing screen, not a bypass of it.

The share buttons need a secure context (`navigator.share` and `navigator.clipboard`
both do), so over plain http they are hidden rather than left as dead buttons — the
QR, the game code and the "open *site* and enter *code*" line work regardless.

## Checklist

- [x] Home page: `🤠 Host & play` (creates + joins) alongside `📺 Host on a big screen`,
      each with a one-line hint saying which room it is for — that hint is where the
      phone-only flow is explained, so the rules page stays about the rules
- [x] Phone lobby: `InvitePanel` — QR, code, share link (`navigator.share`, clipboard
      fallback). Open by default only while the host waits alone; `alone` seeds the
      initial state and nothing more, so a lobby broadcast can't fold it under a host
      who opened it for a latecomer
- [x] Monitor: the QR opens in a new tab, so tapping it no longer nukes the monitor
- [x] Integration test: `HostAndPlay_RunsTheWholeGame_FromTheHostSeat_WithNoMonitor`,
      plus `StartGame_FromANonHostSeat_IsRejected` (starting was never covered)
- [x] Phone → TV handoff: `RequestMonitor` / `DecideMonitor` on the hub, a pairing
      screen on the monitor page, an Allow/Deny prompt on the host's phone in every
      phase (a TV can be wheeled in mid-game). Covered by `MonitorHandoffTests`

### Nobody is left waiting

Every way a request can end tells the screen that made it, because the alternative is
a TV stuck on *"waiting for the host"* with no way out:

| What happens | The waiting screen sees |
|---|---|
| Host taps no | "The host didn't add this screen." |
| Host starts the game instead | "The game started — ask again when it ends." |
| Nobody answers (`Game:MonitorRequestLifetime`, default 2 min) | "No answer from the host — ask again." |
| A game is already running when it asks | "A board goes up between games — ask again when this one ends." |

All four are retriable — the screen keeps an **Ask again** button. The lifetime ships
to the client as *seconds left*, not as an instant, so a device with a skewed clock
still expires it at the right moment; both the waiting screen and the host's prompt
run that clock down, so the host never keeps an Allow button that can only fail.

## Still open

- The host's phone keeps the monitor token from `CreateGame`, so *that device* can
  always open `/monitor/CODE` without pairing. Correct (it is the screen that made
  the game), but it means the pairing path only exercises on a second device — worth
  remembering when testing by hand in one browser, where `localStorage` is shared.
- Only the host's phone renders the Allow/Deny prompt. The server also accepts a
  monitor token there, so a second big screen could be approved from the first, but
  no UI offers it.

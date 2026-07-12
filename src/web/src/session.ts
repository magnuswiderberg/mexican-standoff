// A player's seat in a game, persisted so refresh/drop reconnects them
// (docs/tech-stack.md: secret player token).
//
// Two storages, so tabs are isolated players (dev multi-seat testing) without
// losing the QR-rescan rejoin:
// - sessionStorage is the seat's per-tab home — two tabs in one browser are
//   two independent players.
// - localStorage is the device backup so a phone that closed its tab can
//   rescan the QR and rebind to the same seat. Before a new tab adopts it, a
//   BroadcastChannel probe checks no live tab still holds that seat — only an
//   unanswered probe (the old tab is gone) may take it.

export interface Seat {
  playerId: string
  token: string
  name: string
}

const key = (code: string) => `standoff-seat-${code.toUpperCase()}`
const channelName = (code: string) => `standoff-seat-hold-${code.toUpperCase()}`

const PROBE_TIMEOUT_MS = 250

// One held seat per tab (a tab renders one game page at a time).
let holder: BroadcastChannel | null = null

function read(storage: Storage, code: string): Seat | null {
  try {
    const raw = storage.getItem(key(code))
    return raw ? (JSON.parse(raw) as Seat) : null
  } catch {
    return null
  }
}

/** Start answering "is this seat live in another tab?" probes for our seat. */
function hold(code: string, token: string): void {
  releaseSeatHold()
  try {
    const channel = new BroadcastChannel(channelName(code))
    channel.onmessage = (e: MessageEvent) => {
      if (e.data?.type === 'probe' && e.data?.token === token) {
        channel.postMessage({ type: 'held', token })
      }
    }
    holder = channel
  } catch {
    // No BroadcastChannel: probes go unanswered and the newest tab wins the
    // backup seat — the pre-isolation behavior.
  }
}

/** Stop answering probes (page unmount) so a later tab may adopt the backup. */
export function releaseSeatHold(): void {
  holder?.close()
  holder = null
}

/** Resolves true if a live tab answers for this token within the probe window. */
function heldElsewhere(code: string, token: string): Promise<boolean> {
  return new Promise((resolve) => {
    let channel: BroadcastChannel
    try {
      channel = new BroadcastChannel(channelName(code))
    } catch {
      resolve(false)
      return
    }
    const finish = (held: boolean) => {
      channel.close()
      resolve(held) // a second resolve (timeout after an answer) is a no-op
    }
    channel.onmessage = (e: MessageEvent) => {
      if (e.data?.type === 'held' && e.data?.token === token) finish(true)
    }
    channel.postMessage({ type: 'probe', token })
    setTimeout(() => finish(false), PROBE_TIMEOUT_MS)
  })
}

/**
 * This tab's seat: its own (sessionStorage) instantly, else the device backup
 * (localStorage) if no live tab holds it. Adopting the backup makes this tab
 * the holder. (Two tabs probing at the same instant could both adopt — fine
 * for the rescan/dev flows this serves.)
 */
export async function loadSeat(code: string): Promise<Seat | null> {
  const own = read(sessionStorage, code)
  if (own) {
    hold(code, own.token)
    return own
  }
  const backup = read(localStorage, code)
  if (!backup) return null
  if (await heldElsewhere(code, backup.token)) return null
  saveSeat(code, backup)
  return backup
}

export function saveSeat(code: string, seat: Seat): void {
  try {
    sessionStorage.setItem(key(code), JSON.stringify(seat))
  } catch {
    // Private-mode storage failures just cost the reconnect convenience.
  }
  try {
    localStorage.setItem(key(code), JSON.stringify(seat))
  } catch {
    // ignore
  }
  hold(code, seat.token)
}

export function clearSeat(code: string): void {
  releaseSeatHold()
  const own = read(sessionStorage, code)
  const backup = read(localStorage, code)
  try {
    sessionStorage.removeItem(key(code))
  } catch {
    // ignore
  }
  // The backup slot is last-writer-wins across tabs — leave it alone if it
  // belongs to another tab's seat (kicking tab B must not delete tab A's
  // rejoin backup).
  if (backup && own && backup.token !== own.token) return
  try {
    localStorage.removeItem(key(code))
  } catch {
    // ignore
  }
}

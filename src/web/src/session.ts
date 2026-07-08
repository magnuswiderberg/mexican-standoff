// A player's seat in a game, persisted so refresh/drop reconnects them
// (docs/tech-stack.md: secret player token in localStorage).

export interface Seat {
  playerId: string
  token: string
  name: string
}

const key = (code: string) => `standoff-seat-${code.toUpperCase()}`

export function getSeat(code: string): Seat | null {
  try {
    const raw = localStorage.getItem(key(code))
    return raw ? (JSON.parse(raw) as Seat) : null
  } catch {
    return null
  }
}

export function saveSeat(code: string, seat: Seat): void {
  try {
    localStorage.setItem(key(code), JSON.stringify(seat))
  } catch {
    // Private-mode storage failures just cost the reconnect convenience.
  }
}

export function clearSeat(code: string): void {
  try {
    localStorage.removeItem(key(code))
  } catch {
    // ignore
  }
}

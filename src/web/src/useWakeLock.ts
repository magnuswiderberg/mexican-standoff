import { useEffect } from 'react'

/**
 * Holds the screen awake for as long as the calling page is mounted. Phones dim
 * and lock while the table argues, and waking one back up costs you the round —
 * the selection timer doesn't wait.
 *
 * The browser drops the lock whenever the tab stops being visible, so we
 * re-request it on the way back. Where the API is missing (Firefox, iOS < 16.4)
 * or the request is refused (battery saver), this is a no-op — the game plays
 * exactly as before.
 */
export function useWakeLock(): void {
  useEffect(() => {
    if (!('wakeLock' in navigator)) return

    let sentinel: WakeLockSentinel | null = null
    let released = false

    const acquire = async () => {
      if (released || sentinel !== null || document.visibilityState !== 'visible') return
      try {
        const held = await navigator.wakeLock.request('screen')
        // Unmounted while the request was in flight — don't leak the lock.
        if (released) {
          void held.release()
          return
        }
        sentinel = held
        // Fires on tab-hide too; clearing it lets visibilitychange re-acquire.
        held.addEventListener('release', () => {
          if (sentinel === held) sentinel = null
        })
      } catch {
        // Refused (battery saver, no visible document) — nothing we can do.
      }
    }

    void acquire()
    document.addEventListener('visibilitychange', acquire)

    return () => {
      released = true
      document.removeEventListener('visibilitychange', acquire)
      void sentinel?.release()
    }
  }, [])
}

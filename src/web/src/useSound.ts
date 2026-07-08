import { useCallback, useRef, useState } from 'react'
import { playSfx } from './sound'
import type { SfxName } from './sound'

export interface Sound {
  enabled: boolean
  toggle(): void
  /** Plays an effect if sound is enabled; safe to call from timers. */
  play(name: SfxName): void
}

/**
 * Mute-aware sound switch, persisted per page kind: the monitor carries the
 * room (default on), phones stay silent unless the player opts in.
 */
export function useSound(page: 'player' | 'monitor'): Sound {
  const key = `standoff-sound-${page}`
  const [enabled, setEnabled] = useState<boolean>(() => {
    const stored = localStorage.getItem(key)
    return stored !== null ? stored === '1' : page === 'monitor'
  })
  const enabledRef = useRef(enabled)
  enabledRef.current = enabled

  const play = useCallback((name: SfxName) => {
    if (enabledRef.current) playSfx(name)
  }, [])

  const toggle = useCallback(() => {
    setEnabled((was) => {
      localStorage.setItem(key, was ? '0' : '1')
      // Confirmation blip on unmute — the click gesture also unlocks WebAudio.
      if (!was) playSfx('load')
      return !was
    })
  }, [key])

  return { enabled, toggle, play }
}

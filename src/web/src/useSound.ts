import { useCallback, useEffect, useRef, useState } from 'react'
import { playSfx } from './sound'
import type { SfxName } from './sound'
import type { LobbyView } from './types'

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

/**
 * Chimes when a player joins or leaves the lobby list. Watches seat ids, not
 * counts, so a simultaneous join+leave plays both. The first list seen after
 * entering the lobby (hydrate, rematch return) is a baseline, not a chime.
 */
export function useLobbyChime(phase: string, lobby: LobbyView | null, play: Sound['play']): void {
  const prevIds = useRef<Set<string> | null>(null)
  useEffect(() => {
    if (phase !== 'lobby' && phase !== 'joining') {
      prevIds.current = null
      return
    }
    const ids = new Set(lobby?.players.map((p) => p.id) ?? [])
    const prev = prevIds.current
    prevIds.current = ids
    if (prev === null) return
    if ([...ids].some((id) => !prev.has(id))) play('join')
    if ([...prev].some((id) => !ids.has(id))) play('leave')
  }, [phase, lobby, play])
}

import { useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import type { ActionDto, ActionType, GameSnapshot } from '../types'
import { accentOf } from '../avatars'
import { AttackIcon, BulletIcon, ChestIcon, DodgeIcon, LoadIcon, SkullIcon } from './icons'

/**
 * Final Duel: program a full sequence of actions up front. Legality mirrors
 * DuelResolver.ValidateSequence — validated against the *projected* bullet
 * count within the sequence (Load then Attack on an empty gun is fine).
 * Attack always targets the single opponent; sudden death grants a free
 * bullet before the sequence and removes the chest.
 */
export function DuelPlanner({
  snapshot,
  playerId,
  error,
  onSubmit,
}: {
  snapshot: GameSnapshot
  playerId: string
  error: string | null
  onSubmit: (sequence: ActionDto[]) => void
}) {
  const length = snapshot.duelSequenceLength
  const [steps, setSteps] = useState<(ActionType | null)[]>(() => Array(length).fill(null))
  const [submitting, setSubmitting] = useState(false)

  useEffect(() => {
    if (error) setSubmitting(false)
  }, [error])

  const me = snapshot.players.find((p) => p.id === playerId)!
  const opponent = snapshot.players.find((p) => p.isAlive && p.id !== playerId)!

  const startBullets = snapshot.suddenDeath
    ? Math.min(me.bullets + 1, snapshot.maxBullets)
    : me.bullets

  /** Projected bullets entering each step, given the choices so far. */
  const bulletsBefore = (stepIndex: number): number => {
    let bullets = startBullets
    for (let i = 0; i < stepIndex; i++) {
      if (steps[i] === 'load') bullets = Math.min(bullets + 1, snapshot.maxBullets)
      if (steps[i] === 'attack') bullets = Math.max(0, bullets - 1)
    }
    return bullets
  }

  const optionsFor = (stepIndex: number): { type: ActionType; icon: ReactNode; ok: boolean }[] => {
    const bullets = bulletsBefore(stepIndex)
    return [
      { type: 'dodge', icon: <DodgeIcon />, ok: true },
      { type: 'attack', icon: <AttackIcon />, ok: bullets >= 1 },
      { type: 'load', icon: <LoadIcon />, ok: bullets < snapshot.maxBullets },
      { type: 'chest', icon: <ChestIcon />, ok: snapshot.chestCount > 0 },
    ]
  }

  const setStep = (stepIndex: number, type: ActionType) => {
    if (submitting) return
    const next = [...steps]
    next[stepIndex] = type
    // Later picks may have become illegal under the new projection — clear them.
    for (let i = stepIndex + 1; i < length; i++) {
      const chosen = next[i]
      if (chosen === null) continue
      const stillOk = (() => {
        let bullets = startBullets
        for (let j = 0; j < i; j++) {
          if (next[j] === 'load') bullets = Math.min(bullets + 1, snapshot.maxBullets)
          if (next[j] === 'attack') bullets = Math.max(0, bullets - 1)
        }
        if (chosen === 'attack') return bullets >= 1
        if (chosen === 'load') return bullets < snapshot.maxBullets
        return true
      })()
      if (!stillOk) next[i] = null
    }
    setSteps(next)
  }

  const missing = steps.filter((s) => s === null).length
  const complete = missing === 0

  const lockIn = () => {
    if (!complete || submitting) return
    setSubmitting(true)
    onSubmit(
      steps.map((type): ActionDto => {
        switch (type!) {
          case 'attack':
            return { type: 'attack', targetId: opponent.id }
          case 'chest':
            return { type: 'chest', chestIndex: 0 }
          default:
            return { type: type! }
        }
      }),
    )
  }

  return (
    <div className="picker duel-planner">
      <div className="duel-banner">
        {snapshot.players.length === 2 ? 'Duel' : 'Final Duel'} vs{' '}
        <strong style={{ color: accentOf(opponent.avatar) }}>{opponent.name}</strong>
        {snapshot.suddenDeath && (
          <div className="sudden-death">
            <SkullIcon /> Sudden death — free bullet, no chest!
          </div>
        )}
      </div>
      <p className="hint">
        Program all {length} moves, then watch them play out. Attack needs a bullet in the gun —
        Load first. The count next to each move shows your bullets going into it.
      </p>

      {steps.map((chosen, i) => (
        <div key={i} className="duel-step">
          <span className="duel-step-no">
            {i + 1}
            <span className="duel-step-ammo" title="Bullets in your gun at this move">
              <BulletIcon />
              {bulletsBefore(i)}
            </span>
          </span>
          <div className="duel-options">
            {optionsFor(i).map((opt) => (
              <button
                key={opt.type}
                className={`duel-option ${chosen === opt.type ? 'card-picked' : ''}`}
                disabled={!opt.ok}
                onClick={() => setStep(i, opt.type)}
              >
                {opt.icon} {opt.type === 'chest' ? 'Chest' : opt.type[0].toUpperCase() + opt.type.slice(1)}
              </button>
            ))}
          </div>
        </div>
      ))}

      {error && <div className="error">{error}</div>}

      {/* Say why Lock in is greyed out — dimming alone reads as "broken button". */}
      {!complete && (
        <p className="hint center-text">
          {missing === length
            ? 'Program your moves first.'
            : `${missing} more ${missing === 1 ? 'move' : 'moves'} to program.`}
        </p>
      )}

      <button className="primary lock-in" disabled={!complete || submitting} onClick={lockIn}>
        {submitting ? 'Locking in…' : 'Lock in sequence'}
      </button>
    </div>
  )
}

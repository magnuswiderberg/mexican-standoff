import { useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import type { ActionDto, ActionType, GameSnapshot } from '../types'
import { Avatar } from './PlayerBoard'
import { AttackIcon, ChestIcon, DodgeIcon, LoadIcon, TargetIcon } from './icons'

interface CardSpec {
  type: ActionType
  icon: ReactNode
  label: string
  disabledReason: string | null
  needsTarget: 'player' | 'chest' | null
}

/** Mirrors ActionValidator so illegal cards are disabled up front. */
export function cardsFor(snapshot: GameSnapshot, playerId: string): CardSpec[] {
  const me = snapshot.players.find((p) => p.id === playerId)!
  const opponents = snapshot.players.filter((p) => p.isAlive && p.id !== playerId)
  return [
    { type: 'dodge', icon: <DodgeIcon />, label: 'Dodge', disabledReason: null, needsTarget: null },
    {
      type: 'attack',
      icon: <AttackIcon />,
      label: 'Attack',
      disabledReason:
        me.bullets < 1 ? 'gun is empty' : opponents.length === 0 ? 'no one to shoot' : null,
      needsTarget: 'player',
    },
    {
      type: 'load',
      icon: <LoadIcon />,
      label: 'Load',
      disabledReason: me.bullets >= snapshot.maxBullets ? 'gun is full' : null,
      needsTarget: null,
    },
    {
      type: 'chest',
      icon: <ChestIcon />,
      label: 'Chest',
      disabledReason: snapshot.chestCount === 0 ? 'no chest in play' : null,
      needsTarget: snapshot.chestCount > 1 ? 'chest' : null,
    },
  ]
}

/**
 * One action card + target for the round. Selecting a card that needs a target
 * expands an inline picker; "Lock in" submits.
 */
export function ActionPicker({
  snapshot,
  playerId,
  error,
  onSubmit,
}: {
  snapshot: GameSnapshot
  playerId: string
  error: string | null
  onSubmit: (action: ActionDto) => void
}) {
  const [picked, setPicked] = useState<ActionType | null>(null)
  const [targetId, setTargetId] = useState<string | null>(null)
  const [chestIndex, setChestIndex] = useState<number | null>(null)
  const [submitting, setSubmitting] = useState(false)

  // A server-side rejection re-enables the picker.
  useEffect(() => {
    if (error) setSubmitting(false)
  }, [error])

  const cards = cardsFor(snapshot, playerId)
  const opponents = snapshot.players.filter((p) => p.isAlive && p.id !== playerId)
  const pickedCard = cards.find((c) => c.type === picked) ?? null

  const ready =
    pickedCard !== null &&
    (pickedCard.needsTarget !== 'player' || targetId !== null) &&
    (pickedCard.type !== 'chest' || snapshot.chestCount === 1 || chestIndex !== null)

  const pick = (card: CardSpec) => {
    if (card.disabledReason || submitting) return
    setPicked(card.type)
    setTargetId(null)
    setChestIndex(card.type === 'chest' && snapshot.chestCount === 1 ? 0 : null)
  }

  const lockIn = () => {
    if (!ready || !pickedCard || submitting) return
    setSubmitting(true)
    const action: ActionDto =
      pickedCard.type === 'attack'
        ? { type: 'attack', targetId }
        : pickedCard.type === 'chest'
          ? { type: 'chest', chestIndex: chestIndex ?? 0 }
          : { type: pickedCard.type }
    onSubmit(action)
  }

  return (
    <div className="picker">
      <div className="cards">
        {cards.map((card) => (
          <button
            key={card.type}
            className={`card ${picked === card.type ? 'card-picked' : ''}`}
            disabled={card.disabledReason !== null}
            onClick={() => pick(card)}
          >
            <span className="card-icon">{card.icon}</span>
            <span className="card-label">{card.label}</span>
            {card.disabledReason && <span className="card-why">{card.disabledReason}</span>}
          </button>
        ))}
      </div>

      {pickedCard?.needsTarget === 'player' && (
        <div className="targets">
          <div className="targets-title">Shoot who?</div>
          {opponents.map((p) => (
            <button
              key={p.id}
              className={`target ${targetId === p.id ? 'target-picked' : ''}`}
              onClick={() => setTargetId(p.id)}
            >
              <TargetIcon /> <Avatar avatar={p.avatar} name={p.name} /> {p.name}
            </button>
          ))}
        </div>
      )}

      {pickedCard?.needsTarget === 'chest' && (
        <div className="targets">
          <div className="targets-title">Which chest?</div>
          {Array.from({ length: snapshot.chestCount }, (_, i) => (
            <button
              key={i}
              className={`target ${chestIndex === i ? 'target-picked' : ''}`}
              onClick={() => setChestIndex(i)}
            >
              <ChestIcon /> Chest {i + 1}
            </button>
          ))}
        </div>
      )}

      {error && <div className="error">{error}</div>}

      {/* Say why Lock in is greyed out — dimming alone reads as "broken button". */}
      {!ready && (
        <p className="hint center-text">
          {pickedCard === null
            ? 'Pick an action first.'
            : pickedCard.needsTarget === 'player'
              ? 'Pick who to shoot.'
              : 'Pick a chest.'}
        </p>
      )}

      <button className="primary lock-in" disabled={!ready || submitting} onClick={lockIn}>
        {submitting ? 'Locking in…' : 'Lock in'}
      </button>
    </div>
  )
}

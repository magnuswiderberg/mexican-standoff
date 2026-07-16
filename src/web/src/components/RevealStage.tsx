import { useEffect, useMemo, useRef, useState } from 'react'
import type { RevealJob } from '../useGame'
import type { RevealStepDto } from '../types'
import type { SfxName } from '../sound'
import { applyStep, initialDisplayState, stepDuration } from '../reveal'
import { accentOf } from '../avatars'
import { PlayerBoard } from './PlayerBoard'
import type { ShotFx, StageFx } from './PlayerBoard'
import { Confetti } from './Confetti'
import { ContestedChestIcon, GoldBarIcon, SkullIcon, StarIcon } from './icons'

/**
 * Big stage icon for the dramatic beats; null for quiet steps. Single-actor
 * beats (a chest grab) anchor to that player's tile; multi-actor beats
 * (standoff, elimination + skull-on-tile, victory) stay center stage.
 */
function stageFx(step: RevealStepDto, key: number): StageFx | null {
  switch (step.type) {
    case 'chestResolved':
      if (step.chestWinnerId) return { icon: <GoldBarIcon className="fx-gold" />, playerId: step.chestWinnerId, key }
      if ((step.contenderIds?.length ?? 0) > 1) return { icon: <ContestedChestIcon className="fx-danger" />, playerId: null, key }
      return null
    case 'playerEliminated':
      return { icon: <SkullIcon />, playerId: null, key }
    case 'gameEnded':
      return {
        icon: (step.winnerIds?.length ?? 0) > 0 ? <StarIcon className="fx-gold" /> : <SkullIcon />,
        playerId: null,
        key,
      }
    default:
      return null
  }
}

/** Sounds a step triggers: effect name plus optional delay in ms. */
function stepSounds(step: RevealStepDto): [SfxName, number][] {
  switch (step.type) {
    case 'actionsRevealed':
      return [['flip', 0]]
    case 'shotFired':
      return step.hit
        ? [['shot', 0]]
        : [
            ['shot', 0],
            ['whoosh', 280],
          ]
    case 'actionCancelled':
      // A shot-off heal also clinks the wasted gold away after the fizzle.
      return step.action?.type === 'heal' && (step.goldLost ?? 0) > 0
        ? [
            ['cancel', 0],
            ['coin', 260],
          ]
        : [['cancel', 0]]
    case 'actionFizzled':
      return [['cancel', 0]]
    case 'gunLoaded':
    case 'suddenDeathBullet':
      return [['load', 0]]
    case 'playerHealed':
      // The heal chime, and — under no-refund — a coin clink for the bars spent.
      return (step.goldLost ?? 0) > 0
        ? [
            ['heal', 0],
            ['coin', 320],
          ]
        : [['heal', 0]]
    case 'chestResolved':
      if (step.chestWinnerId) return [['gold', 0]]
      if ((step.contenderIds?.length ?? 0) > 1) return [['standoff', 0]]
      return []
    case 'playerEliminated':
      return (step.goldPerLooter ?? 0) > 0
        ? [
            ['eliminated', 0],
            ['gold', 700],
          ]
        : [['eliminated', 0]]
    case 'gameEnded':
      // Mutual destruction ends with no winner: a somber beat, no fanfare.
      return (step.winnerIds?.length ?? 0) > 0 ? [['fanfare', 0]] : [['eliminated', 0]]
    default:
      return []
  }
}

/**
 * Plays a reveal script step by step on a timer. All devices receive the same
 * script at (nearly) the same moment and use the same durations, so phones and
 * the monitor animate in lockstep. Calls `onDone` after the last step's pause.
 * Sound is per-device (mute-aware) and never affects the shared timing.
 */
export function RevealStage({
  job,
  meId,
  maxHp,
  onDone,
  playSound,
}: {
  job: RevealJob
  meId?: string | null
  /** HP-pip ceiling — the game's MaxHp (above the starting HP when healing is on). */
  maxHp: number
  onDone: () => void
  playSound?: (name: SfxName) => void
}) {
  const [stepIndex, setStepIndex] = useState(-1) // -1: brief beat before the card flip
  const onDoneRef = useRef(onDone)
  onDoneRef.current = onDone
  const soundRef = useRef(playSound)
  soundRef.current = playSound

  useEffect(() => {
    setStepIndex(-1)
    const timers: ReturnType<typeof setTimeout>[] = []
    // Anchor every step to one start point rather than chaining each timer off the
    // previous callback. A relative chain compounds lateness — a busy frame delays
    // the tick, which arms the next tick even later — and the heavier renderer (the
    // monitor) drifts further behind the phones with every step. Absolute offsets
    // let a device that falls behind catch back up to the shared timeline instead.
    const start = performance.now()

    const showStep = (index: number) => {
      setStepIndex(index)
      const step = job.steps[index]
      for (const [name, delay] of stepSounds(step)) {
        timers.push(setTimeout(() => soundRef.current?.(name), delay))
      }
    }

    let offset = 600 // brief beat before the card flip
    for (let i = 0; i < job.steps.length; i++) {
      const at = offset
      timers.push(setTimeout(() => showStep(i), Math.max(0, start + at - performance.now())))
      offset += stepDuration(job.steps[i])
    }
    timers.push(setTimeout(() => onDoneRef.current(), Math.max(0, start + offset - performance.now())))
    return () => timers.forEach(clearTimeout)
  }, [job])

  const display = useMemo(() => {
    let state = initialDisplayState(job.prev)
    for (let i = 0; i <= stepIndex && i < job.steps.length; i++) {
      state = applyStep(state, job.steps[i])
    }
    return state
  }, [job, stepIndex])

  const colorById = useMemo(
    () => new Map(job.prev.players.map((p) => [p.id, accentOf(p.avatar)])),
    [job],
  )

  const current = stepIndex >= 0 && stepIndex < job.steps.length ? job.steps[stepIndex] : null
  const shot: ShotFx | null =
    current?.type === 'shotFired' && current.shooterId && current.targetId
      ? { shooterId: current.shooterId, targetId: current.targetId, hit: current.hit ?? false, key: stepIndex }
      : null
  const stage = current ? stageFx(current, stepIndex) : null

  return (
    <div className="reveal-stage">
      <div className={`caption caption-${current?.type ?? 'waiting'}`} key={stepIndex}>
        {/* One inline wrapper: the caption is a flex box, and separate flex
            items would swallow the spaces between caption parts. */}
        <span className="caption-text">
          {stepIndex < 0
            ? `Round ${job.prev.roundNumber + 1} — showdown!`
            : display.caption.map((part, i) =>
                part.playerId ? (
                  <span key={i} className="caption-name" style={{ color: colorById.get(part.playerId) }}>
                    {part.text}
                  </span>
                ) : (
                  <span key={i}>{part.text}</span>
                ),
              )}
        </span>
      </div>
      <div className="reveal-board">
        <PlayerBoard
          players={display.players}
          maxHp={maxHp}
          maxBullets={job.prev.maxBullets}
          goldToWin={job.prev.goldToWin}
          chestCount={job.prev.chestCount}
          meId={meId}
          shot={shot}
          stage={stage}
          reserveAction
          animKey={stepIndex}
        />
      </div>
      {display.winnerIds !== null && display.winnerIds.length > 0 && <Confetti />}
      <div className="reveal-progress">
        {job.steps.map((_, i) => (
          <span
            key={i}
            className={i < stepIndex ? 'dot dot-done' : i === stepIndex ? 'dot dot-done dot-now' : 'dot'}
          />
        ))}
      </div>
    </div>
  )
}

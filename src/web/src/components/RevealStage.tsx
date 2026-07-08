import { useEffect, useMemo, useRef, useState } from 'react'
import type { RevealJob } from '../useGame'
import { applyStep, initialDisplayState, stepDuration } from '../reveal'
import { PlayerBoard } from './PlayerBoard'

/**
 * Plays a reveal script step by step on a timer. All devices receive the same
 * script at (nearly) the same moment and use the same durations, so phones and
 * the monitor animate in lockstep. Calls `onDone` after the last step's pause.
 */
export function RevealStage({
  job,
  meId,
  startingHp,
  onDone,
}: {
  job: RevealJob
  meId?: string | null
  startingHp: number
  onDone: () => void
}) {
  const [stepIndex, setStepIndex] = useState(-1) // -1: brief beat before the card flip
  const onDoneRef = useRef(onDone)
  onDoneRef.current = onDone

  useEffect(() => {
    setStepIndex(-1)
    let index = -1
    let timer: ReturnType<typeof setTimeout>

    const tick = () => {
      index += 1
      if (index >= job.steps.length) {
        onDoneRef.current()
        return
      }
      setStepIndex(index)
      timer = setTimeout(tick, stepDuration(job.steps[index]))
    }

    timer = setTimeout(tick, 600)
    return () => clearTimeout(timer)
  }, [job])

  const display = useMemo(() => {
    let state = initialDisplayState(job.prev)
    for (let i = 0; i <= stepIndex && i < job.steps.length; i++) {
      state = applyStep(state, job.steps[i])
    }
    return state
  }, [job, stepIndex])

  const current = stepIndex >= 0 && stepIndex < job.steps.length ? job.steps[stepIndex] : null

  return (
    <div className="reveal-stage">
      <div className={`caption caption-${current?.type ?? 'waiting'}`} key={stepIndex}>
        {stepIndex < 0 ? `Round ${job.prev.roundNumber + 1} — showdown!` : display.caption}
      </div>
      <PlayerBoard
        players={display.players}
        maxHp={startingHp}
        maxBullets={job.prev.maxBullets}
        goldToWin={job.prev.goldToWin}
        meId={meId}
      />
      <div className="reveal-progress">
        {job.steps.map((_, i) => (
          <span key={i} className={i <= stepIndex ? 'dot dot-done' : 'dot'} />
        ))}
      </div>
    </div>
  )
}

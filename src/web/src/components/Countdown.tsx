import { useEffect, useRef, useState } from 'react'

const URGENT_AT = 10

function secondsLeft(deadline: string): number {
  return Math.max(0, Math.ceil((new Date(deadline).getTime() - Date.now()) / 1000))
}

export function Countdown({
  deadline,
  onUrgentTick,
}: {
  deadline: string | null
  /** Called once per second while the countdown is urgent (e.g. a tick sound). */
  onUrgentTick?: () => void
}) {
  const [left, setLeft] = useState(() => (deadline ? secondsLeft(deadline) : null))
  const onUrgentTickRef = useRef(onUrgentTick)
  onUrgentTickRef.current = onUrgentTick
  const lastTickedRef = useRef<number | null>(null)

  useEffect(() => {
    if (!deadline) {
      setLeft(null)
      return
    }
    setLeft(secondsLeft(deadline))
    const timer = setInterval(() => setLeft(secondsLeft(deadline)), 250)
    return () => clearInterval(timer)
  }, [deadline])

  useEffect(() => {
    if (left === null || left > URGENT_AT || left <= 0) return
    if (lastTickedRef.current === left) return
    lastTickedRef.current = left
    onUrgentTickRef.current?.()
  }, [left])

  if (left === null) return null
  return <div className={left <= URGENT_AT ? 'countdown countdown-urgent' : 'countdown'}>⏱ {left}s</div>
}

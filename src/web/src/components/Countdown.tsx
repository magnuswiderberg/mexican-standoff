import { useEffect, useState } from 'react'

function secondsLeft(deadline: string): number {
  return Math.max(0, Math.ceil((new Date(deadline).getTime() - Date.now()) / 1000))
}

export function Countdown({ deadline }: { deadline: string | null }) {
  const [left, setLeft] = useState(() => (deadline ? secondsLeft(deadline) : null))

  useEffect(() => {
    if (!deadline) {
      setLeft(null)
      return
    }
    setLeft(secondsLeft(deadline))
    const timer = setInterval(() => setLeft(secondsLeft(deadline)), 250)
    return () => clearInterval(timer)
  }, [deadline])

  if (left === null) return null
  return <div className={left <= 10 ? 'countdown countdown-urgent' : 'countdown'}>⏱ {left}s</div>
}

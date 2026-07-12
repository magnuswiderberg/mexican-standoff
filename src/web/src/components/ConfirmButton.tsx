import { useEffect, useState } from 'react'

/**
 * Two-tap button for destructive actions (resign, stop game): the first tap
 * arms it, the second fires. Disarms by itself after a few seconds.
 */
export function ConfirmButton({
  label,
  confirmLabel,
  onConfirm,
  className = '',
}: {
  label: string
  confirmLabel: string
  onConfirm: () => void
  className?: string
}) {
  const [armed, setArmed] = useState(false)

  useEffect(() => {
    if (!armed) return
    const timer = setTimeout(() => setArmed(false), 4000)
    return () => clearTimeout(timer)
  }, [armed])

  const click = () => {
    if (armed) {
      setArmed(false)
      onConfirm()
    } else {
      setArmed(true)
    }
  }

  return (
    <button type="button" className={`secondary ${className} ${armed ? 'confirm-armed' : ''}`} onClick={click}>
      {armed ? confirmLabel : label}
    </button>
  )
}

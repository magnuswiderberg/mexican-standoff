import type { CSSProperties } from 'react'
import { ACCENTS } from '../avatars'

/**
 * CSS-only confetti rain. Pieces are laid out with index-derived pseudo-random
 * values, so it is cheap, deterministic, and needs no assets. Purely visual —
 * not part of the lockstep reveal timing.
 */
export function Confetti({ pieces = 90 }: { pieces?: number }) {
  return (
    <div className="confetti" aria-hidden>
      {Array.from({ length: pieces }, (_, i) => {
        const rand = (salt: number) => (((i + 7) * salt) % 101) / 101
        const style: CSSProperties = {
          left: `${rand(37) * 100}%`,
          background: ACCENTS[i % ACCENTS.length],
          animationDelay: `${rand(53) * 2.4}s`,
          animationDuration: `${2.6 + rand(29) * 2.2}s`,
          width: `${0.4 + rand(17) * 0.5}rem`,
          height: `${0.55 + rand(23) * 0.6}rem`,
          ['--sway' as string]: `${(rand(41) - 0.5) * 14}rem`,
          ['--spin' as string]: `${360 + rand(61) * 540}deg`,
        }
        return <span key={i} className="confetti-piece" style={style} />
      })}
    </div>
  )
}

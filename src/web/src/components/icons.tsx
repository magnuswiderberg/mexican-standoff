import type { ReactNode } from 'react'

/**
 * Hand-drawn western icon set for the four actions plus the ammo pip.
 * Emoji sold the theme short (🔺 read as a warning sign, 🔫 as a water
 * pistol) and rendered differently per platform; these draw in currentColor
 * at 1em so they inherit size and color like the glyphs they replaced.
 */
function Icon({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <svg
      className={className ? `icon ${className}` : 'icon'}
      viewBox="0 0 24 24"
      fill="currentColor"
      aria-hidden="true"
      focusable="false"
    >
      {children}
    </svg>
  )
}

/** Dodge: a gust of dust. */
export function DodgeIcon({ className }: { className?: string }) {
  return (
    <Icon className={className}>
      <g fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
        <path d="M9.6 4.6A2 2 0 1 1 11 8H2" />
        <path d="M17.7 7.7A2.5 2.5 0 1 1 19.5 12H2" />
        <path d="M12.6 19.4A2 2 0 1 0 14 16H2" />
      </g>
    </Icon>
  )
}

/** Attack: a six-shooter, muzzle left (the direction the old emoji faced). */
export function AttackIcon({ className }: { className?: string }) {
  return (
    <Icon className={className}>
      {/* front sight */}
      <rect x="2.6" y="4.4" width="1.4" height="2" rx="0.5" />
      {/* barrel + frame */}
      <rect x="2" y="6" width="18" height="3.6" rx="1" />
      {/* hammer spur */}
      <path d="M18.4 6.6 21.2 3.8a1.1 1.1 0 0 1 1.6 1.6l-2.6 2.6z" />
      {/* cylinder */}
      <rect x="9.6" y="4.4" width="5.8" height="7.2" rx="1.4" />
      {/* trigger guard */}
      <circle cx="13.4" cy="12.2" r="2.1" fill="none" stroke="currentColor" strokeWidth="1.7" />
      {/* grip */}
      <rect x="15.7" y="8.8" width="4.1" height="9.4" rx="1.5" transform="rotate(14 17.75 13.5)" />
    </Icon>
  )
}

/** Load: a revolver cylinder seen head-on, chambers full. */
export function LoadIcon({ className }: { className?: string }) {
  return (
    <Icon className={className}>
      <circle cx="12" cy="12" r="9.2" fill="none" stroke="currentColor" strokeWidth="2" />
      <circle cx="12" cy="12" r="1.3" />
      {[90, 150, 210, 270, 330, 30].map((deg) => {
        const rad = (deg * Math.PI) / 180
        return <circle key={deg} cx={12 + 5.1 * Math.cos(rad)} cy={12 + 5.1 * Math.sin(rad)} r="1.8" />
      })}
    </Icon>
  )
}

/** Chest: a round-lidded treasure chest with a lock. */
export function ChestIcon({ className }: { className?: string }) {
  return (
    <Icon className={className}>
      <g fill="none" stroke="currentColor" strokeWidth="2" strokeLinejoin="round">
        <path d="M3.5 10.5Q3.5 4.5 12 4.5T20.5 10.5V18a1.5 1.5 0 0 1-1.5 1.5H5A1.5 1.5 0 0 1 3.5 18Z" />
        <path d="M3.5 10.5h17" />
      </g>
      <rect x="10.3" y="8.6" width="3.4" height="5" rx="1" />
    </Icon>
  )
}

/** Ammo pip: a cartridge standing on its rim. */
export function BulletIcon({ className }: { className?: string }) {
  return (
    <Icon className={className}>
      {/* slug */}
      <path d="M12 1.6q3 2 3 5.8V9H9V7.4q0-3.8 3-5.8z" />
      {/* case */}
      <path d="M8.6 10.2h6.8v7.6q0 1.4-1.4 1.4h-4q-1.4 0-1.4-1.4z" />
      {/* rim */}
      <rect x="7.8" y="20.2" width="8.4" height="2" rx="1" />
    </Icon>
  )
}

import type { ReactNode } from 'react'

/**
 * Hand-drawn western icon set — the four actions, the game-state markers
 * (HP, gold, skull, duel, flag, star, target, hit) and a couple of controls.
 * Emoji sold the theme short (🔺 read as a warning sign, 🔫 as a water pistol),
 * rendered differently per platform, and read a touch childish; these draw in
 * currentColor at 1em so they inherit size and color like the glyphs they
 * replaced. Anything still spelled with a glyph elsewhere is plain text now.
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

/** Gold pip: two stacked ingots (the loot is bars, not coins). */
export function GoldBarIcon({ className }: { className?: string }) {
  return (
    <Icon className={className}>
      {/* top bar */}
      <path d="M7.4 8.4h9.2l1.7 4H5.7z" />
      {/* bottom bar */}
      <path d="M4.6 13.4h14.8l1.7 5H2.9z" />
    </Icon>
  )
}

/** HP pip: a small shield — a hit taken, not a cutesy heart. */
export function HpIcon({ className }: { className?: string }) {
  return (
    <Icon className={className}>
      <path d="M12 2.6 20 5.6V11c0 5-3.4 8.5-8 10-4.6-1.5-8-5-8-10V5.6Z" />
    </Icon>
  )
}

/** Skull: eliminated / mutual destruction. */
export function SkullIcon({ className }: { className?: string }) {
  return (
    <Icon className={className}>
      <path
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinejoin="round"
        d="M12 3C7.3 3 4.5 6.3 4.5 10.2c0 2.4 1.1 4 2.5 5.1v2.2C7 18.3 7.7 19 8.5 19h7c.8 0 1.5-.7 1.5-1.5v-2.2c1.4-1.1 2.5-2.7 2.5-5.1C19.5 6.3 16.7 3 12 3Z"
      />
      <circle cx="9" cy="11" r="1.8" />
      <circle cx="15" cy="11" r="1.8" />
      <path d="M12 12.8 13.1 15h-2.2z" />
      <g stroke="currentColor" strokeWidth="1.4" strokeLinecap="round">
        <path d="M10 19v-2.4" />
        <path d="M12 19.2v-2.4" />
        <path d="M14 19v-2.4" />
      </g>
    </Icon>
  )
}

/**
 * Duel / standoff: two crossed six-shooters — chunky barrels up-and-out,
 * grip blocks down-and-out around a cylinder hub, so it reads as guns (not a
 * bare X) at the large stage size where it's used.
 */
export function DuelIcon({ className }: { className?: string }) {
  return (
    <Icon className={className}>
      {/* barrels */}
      <path d="M3.2 5.2 4.8 3.6 13 11.8 11.4 13.4Z" />
      <path d="M20.8 5.2 19.2 3.6 11 11.8 12.6 13.4Z" />
      {/* grips */}
      <path d="M12.8 12.2 18.4 15.4 16.8 18.4 11.6 15Z" />
      <path d="M11.2 12.2 5.6 15.4 7.2 18.4 12.4 15Z" />
      {/* cylinder hub */}
      <circle cx="12" cy="12" r="2.4" />
    </Icon>
  )
}

/** Surrender flag: resign / walk away. */
export function FlagIcon({ className }: { className?: string }) {
  return (
    <Icon className={className}>
      <path fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" d="M6 3v18" />
      <path d="M6 3.8h11l-3 3.6 3 3.6H6z" />
    </Icon>
  )
}

/** Sheriff star: winner, and the host's signature mark. */
export function StarIcon({ className }: { className?: string }) {
  const outer = 9
  const inner = 3.9
  const tips = [-90, -18, 54, 126, 198]
  const pts: string[] = []
  for (let k = 0; k < 5; k++) {
    const ao = ((k * 72 - 90) * Math.PI) / 180
    const ai = ((k * 72 - 54) * Math.PI) / 180
    pts.push(`${12 + outer * Math.cos(ao)} ${12 + outer * Math.sin(ao)}`)
    pts.push(`${12 + inner * Math.cos(ai)} ${12 + inner * Math.sin(ai)}`)
  }
  return (
    <Icon className={className}>
      <path d={`M${pts.join('L')}Z`} />
      {tips.map((deg) => {
        const a = (deg * Math.PI) / 180
        return <circle key={deg} cx={12 + outer * Math.cos(a)} cy={12 + outer * Math.sin(a)} r="1.3" />
      })}
    </Icon>
  )
}

/** Target reticle: pick who to shoot. */
export function TargetIcon({ className }: { className?: string }) {
  return (
    <Icon className={className}>
      <circle cx="12" cy="12" r="8" fill="none" stroke="currentColor" strokeWidth="2" />
      <circle cx="12" cy="12" r="2" />
      <g stroke="currentColor" strokeWidth="2" strokeLinecap="round">
        <path d="M12 1.5V5" />
        <path d="M12 19v3.5" />
        <path d="M1.5 12H5" />
        <path d="M19 12h3.5" />
      </g>
    </Icon>
  )
}

/** Impact burst: a shot that lands. */
export function HitIcon({ className }: { className?: string }) {
  return (
    <Icon className={className}>
      <path d="M12 2 13.75 8.97 20.66 7 15.5 12 20.66 17 13.75 15.03 12 22 10.25 15.03 3.34 17 8.5 12 3.34 7 10.25 8.97Z" />
    </Icon>
  )
}

/** Sound on: speaker with two waves. */
export function SpeakerOnIcon({ className }: { className?: string }) {
  return (
    <Icon className={className}>
      <path d="M4 9h3l5-4v14l-5-4H4z" />
      <g fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
        <path d="M15.5 9a4 4 0 0 1 0 6" />
        <path d="M18 6.5a7.5 7.5 0 0 1 0 11" />
      </g>
    </Icon>
  )
}

/** Sound off: speaker with a cross. */
export function SpeakerOffIcon({ className }: { className?: string }) {
  return (
    <Icon className={className}>
      <path d="M4 9h3l5-4v14l-5-4H4z" />
      <g fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
        <path d="M16 9.5 21 14.5" />
        <path d="M21 9.5 16 14.5" />
      </g>
    </Icon>
  )
}

/**
 * Avatar roster — mirrors Avatars.All on the server, which stores and dedupes
 * the *keys*; only the frontend knows the portraits and accent colors.
 * Portraits live in public/avatars/<key>.webp (256px, generated from the
 * originals with ImageMagick).
 */

export interface AvatarSpec {
  key: string
  /** Character name, e.g. "Luz Romero". */
  name: string
  /** Nickname, e.g. "La Cascabel". */
  persona: string
  /** Accent color used for tile borders, caption names and banners. */
  accent: string
}

export const AVATARS: AvatarSpec[] = [
  { key: 'forastero', name: 'Vicente Álvarez', persona: 'El Forastero', accent: '#c2543c' },
  { key: 'viuda', name: 'Dolores Mendoza', persona: 'La Viuda', accent: '#9c5a8f' },
  { key: 'enterrador', name: 'Ignacio Barbosa', persona: 'El Enterrador', accent: '#5c9078' },
  { key: 'cascabel', name: 'Luz Romero', persona: 'La Cascabel', accent: '#4a86a8' },
  { key: 'lobo', name: 'El Lobo Gris', persona: 'El Lobo Gris', accent: '#8a8f98' },
  { key: 'tahura', name: 'Rosa Herrera', persona: 'La Tahúra', accent: '#c04a68' },
  { key: 'predicador', name: 'Salvador Quintero', persona: 'El Predicador', accent: '#7b6cae' },
  { key: 'cazadora', name: 'Calista Duarte', persona: 'La Cazadora', accent: '#7f8f4a' },
  { key: 'gambusino', name: 'Joaquín Peralta', persona: 'El Gambusino', accent: '#a05e2c' },
  { key: 'contrabandista', name: 'Perla Escamilla', persona: 'La Contrabandista', accent: '#3e9d8e' },
]

const byKey = new Map(AVATARS.map((a) => [a.key, a]))

export function avatarOf(key: string | null | undefined): AvatarSpec | undefined {
  return byKey.get(key ?? '')
}

/** Accent color for an avatar key; a neutral fallback keeps unknown keys harmless. */
export function accentOf(key: string | null | undefined): string {
  return avatarOf(key)?.accent ?? '#b3a48c'
}

export function avatarUrl(key: string): string {
  return `/avatars/${key}.webp`
}

/** All accents, for confetti and other multi-color effects. */
export const ACCENTS = AVATARS.map((a) => a.accent)

/**
 * Avatar palette — mirrors AvatarColors on the server. The server stores and
 * dedupes the *keys*; only the frontend knows what they look like.
 */

export const AVATAR_COLORS = [
  'red',
  'orange',
  'yellow',
  'green',
  'teal',
  'blue',
  'purple',
  'pink',
] as const

export type AvatarColor = (typeof AVATAR_COLORS)[number]

const CSS: Record<AvatarColor, string> = {
  red: '#e0584a',
  orange: '#e8913c',
  yellow: '#e3c94f',
  green: '#8fbf6a',
  teal: '#4fc4b8',
  blue: '#6fa8dc',
  purple: '#a98bdd',
  pink: '#e78fb8',
}

/** CSS color for a palette key; a neutral fallback keeps unknown keys harmless. */
export function colorOf(key: string | null | undefined): string {
  return CSS[key as AvatarColor] ?? '#b3a48c'
}

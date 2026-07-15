import { useEffect, useState } from 'react'

/** Three routes don't need a router library (payload matters on party wifi). */

export function navigate(to: string): void {
  // The state marker lets pages tell an in-app entry (back stays in the app)
  // from a direct load / new tab, where history.state is null.
  history.pushState({ app: true }, '', to)
  dispatchEvent(new PopStateEvent('popstate'))
}

/** True when the current history entry was pushed by navigate(). */
export function arrivedInApp(): boolean {
  return (history.state as { app?: boolean } | null)?.app === true
}

export function usePath(): string {
  const [path, setPath] = useState(location.pathname)
  useEffect(() => {
    const onPop = () => setPath(location.pathname)
    addEventListener('popstate', onPop)
    return () => removeEventListener('popstate', onPop)
  }, [])
  return path
}

export type Route =
  | { page: 'home' }
  | { page: 'player'; code: string }
  | { page: 'monitor'; code: string }
  | { page: 'howto' }
  | { page: 'icons' }

export function parseRoute(path: string): Route {
  const game = path.match(/^\/game\/([A-Za-z0-9]+)\/?$/)
  if (game) return { page: 'player', code: game[1].toUpperCase() }
  const monitor = path.match(/^\/monitor\/([A-Za-z0-9]+)\/?$/)
  if (monitor) return { page: 'monitor', code: monitor[1].toUpperCase() }
  if (/^\/how-to-play\/?$/.test(path)) return { page: 'howto' }
  // Unlisted showcase: the whole icon set on the real theme. Not linked anywhere.
  if (/^\/_icons\/?$/.test(path)) return { page: 'icons' }
  return { page: 'home' }
}

import { useEffect, useState } from 'react'

/** Three routes don't need a router library (payload matters on party wifi). */

export function navigate(to: string): void {
  history.pushState(null, '', to)
  dispatchEvent(new PopStateEvent('popstate'))
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

export function parseRoute(path: string): Route {
  const game = path.match(/^\/game\/([A-Za-z0-9]+)\/?$/)
  if (game) return { page: 'player', code: game[1].toUpperCase() }
  const monitor = path.match(/^\/monitor\/([A-Za-z0-9]+)\/?$/)
  if (monitor) return { page: 'monitor', code: monitor[1].toUpperCase() }
  return { page: 'home' }
}

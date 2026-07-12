import { parseRoute, usePath } from './router'
import { HomePage } from './pages/HomePage'
import { PlayerPage } from './pages/PlayerPage'
import { MonitorPage } from './pages/MonitorPage'
import { HowToPlayPage } from './pages/HowToPlayPage'

export function App() {
  const route = parseRoute(usePath())
  switch (route.page) {
    case 'player':
      // Keyed by code so switching games remounts (fresh connection + state).
      return <PlayerPage key={route.code} code={route.code} />
    case 'monitor':
      return <MonitorPage key={route.code} code={route.code} />
    case 'howto':
      return <HowToPlayPage />
    default:
      return <HomePage />
  }
}

import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr'

export function createConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl('/hub/game')
    .withAutomaticReconnect([0, 1000, 2000, 5000, 10000, 30000])
    .configureLogging(LogLevel.Warning)
    .build()
}

/**
 * Server-side rule violations arrive as HubException messages wrapped in
 * SignalR's invocation-failed prose; strip that down to the human part.
 */
export function friendlyError(e: unknown): string {
  const raw = e instanceof Error ? e.message : String(e)
  const match = raw.match(/HubException: (.*)$/)
  if (match) return match[1]
  // Non-HubException server faults come as a generic "unexpected error" blurb.
  if (raw.includes('An unexpected error occurred')) return 'Something went wrong on the server.'
  if (raw.includes('Failed to fetch') || raw.includes('negotiation')) return 'Cannot reach the game server.'
  return raw
}

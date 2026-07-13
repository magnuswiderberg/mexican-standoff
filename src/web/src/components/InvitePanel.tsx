import { useState } from 'react'
import QRCode from 'react-qr-code'

/**
 * Both APIs need a secure context, so over plain http (a LAN party without TLS)
 * neither exists and the button would be a no-op that looks broken. Render nothing
 * then: the panel always shows the code and the QR, which is the fallback anyway.
 */
const canShare = typeof navigator.share === 'function' || navigator.clipboard != null

/**
 * Share sheet on a phone (drop the link in the group chat), clipboard everywhere
 * else. A dismissed sheet or a refused clipboard is not worth an error — the code
 * next to this button still gets people in.
 */
function ShareButton({ label, title, text, url }: { label: string; title: string; text: string; url: string }) {
  const [copied, setCopied] = useState(false)

  if (!canShare) return null

  const share = async () => {
    if (navigator.share) {
      try {
        await navigator.share({ title, text, url })
      } catch {
        // cancelled the sheet
      }
      return
    }
    try {
      await navigator.clipboard.writeText(url)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    } catch {
      // no clipboard access
    }
  }

  return (
    <button type="button" className="secondary" onClick={share}>
      {copied ? '✅ Link copied' : label}
    </button>
  )
}

/**
 * How people get into a monitor-less game: the host's own phone is the QR code.
 * Open by default while the host waits alone — that is exactly the moment the
 * code needs to be on screen — and foldable away once the gang has arrived.
 */
export function InvitePanel({ code, alone }: { code: string; alone: boolean }) {
  // `alone` only seeds the initial state: once open, the panel is the player's to
  // fold. Binding <details open> straight to it would slam the panel shut under a
  // host who opened it for a latecomer, on the next lobby broadcast.
  const [open, setOpen] = useState(alone)
  const joinUrl = `${location.origin}/game/${code}`
  const boardUrl = `${location.origin}/monitor/${code}`

  return (
    <details className="invite" open={open} onToggle={(e) => setOpen(e.currentTarget.open)}>
      <summary>📲 Invite players</summary>
      <div className="invite-body">
        <div className="qr-panel">
          <QRCode value={joinUrl} size={200} bgColor="#f5ead6" fgColor="#171310" />
        </div>
        <div className="code code-big">{code}</div>
        <ShareButton
          label="🔗 Share link"
          title="Mexican Standoff"
          text={`Join my standoff — code ${code}`}
          url={joinUrl}
        />

        {/* The TV can't scan a QR and nobody enjoys typing on one, so the host hands
            it the board link instead. It carries no token: the screen that opens it
            still has to ask, and the host still has to say yes. */}
        <div className="invite-board">
          <p className="hint">
            📺 Got a TV or laptop? Open <strong>{location.host}</strong> on it, enter{' '}
            <span className="code">{code}</span> and tap <strong>Show the board</strong>
            {canShare && ' — or just send it the board link'}. You'll get a code to confirm.
          </p>
          <ShareButton
            label="📺 Share board link"
            title="Mexican Standoff — the board"
            text={`Show the Mexican Standoff board (game ${code}) on this screen`}
            url={boardUrl}
          />
        </div>
      </div>
    </details>
  )
}

import type { Sound } from '../useSound'
import { SpeakerOffIcon, SpeakerOnIcon } from './icons'

export function SoundToggle({ sound }: { sound: Sound }) {
  return (
    <button
      className="sound-toggle"
      onClick={sound.toggle}
      aria-label={sound.enabled ? 'Mute sound' : 'Unmute sound'}
      title={sound.enabled ? 'Mute sound' : 'Unmute sound'}
    >
      {sound.enabled ? <SpeakerOnIcon /> : <SpeakerOffIcon />}
    </button>
  )
}

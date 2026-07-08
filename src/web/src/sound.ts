/**
 * Synthesized sound effects — pure WebAudio, no shipped samples. The context
 * is created lazily and unlocked by any pointer/key gesture (browsers keep
 * audio suspended until one); if it is still suspended when an effect fires,
 * the effect is simply skipped.
 */

export type SfxName =
  | 'flip'
  | 'shot'
  | 'whoosh'
  | 'load'
  | 'gold'
  | 'standoff'
  | 'eliminated'
  | 'cancel'
  | 'fanfare'
  | 'tick'

let ctx: AudioContext | null = null
let master: GainNode | null = null

function context(): AudioContext | null {
  if (typeof window === 'undefined' || typeof window.AudioContext === 'undefined') return null
  if (!ctx) {
    ctx = new AudioContext()
    master = ctx.createGain()
    master.gain.value = 0.5
    master.connect(ctx.destination)
    const unlock = () => ctx?.resume().catch(() => {})
    document.addEventListener('pointerdown', unlock)
    document.addEventListener('keydown', unlock)
  }
  if (ctx.state === 'suspended') ctx.resume().catch(() => {})
  return ctx.state === 'running' ? ctx : null
}

interface ToneSpec {
  freq: number
  /** Glide target; defaults to `freq` (no glide). */
  freqEnd?: number
  type?: OscillatorType
  at?: number
  dur: number
  vol?: number
}

function tone(c: AudioContext, spec: ToneSpec) {
  const t0 = c.currentTime + (spec.at ?? 0)
  const osc = c.createOscillator()
  osc.type = spec.type ?? 'sine'
  osc.frequency.setValueAtTime(spec.freq, t0)
  if (spec.freqEnd !== undefined)
    osc.frequency.exponentialRampToValueAtTime(Math.max(1, spec.freqEnd), t0 + spec.dur)
  const gain = c.createGain()
  gain.gain.setValueAtTime(0, t0)
  gain.gain.linearRampToValueAtTime(spec.vol ?? 0.3, t0 + 0.008)
  gain.gain.exponentialRampToValueAtTime(0.0005, t0 + spec.dur)
  osc.connect(gain).connect(master!)
  osc.start(t0)
  osc.stop(t0 + spec.dur + 0.05)
}

interface NoiseSpec {
  at?: number
  dur: number
  vol?: number
  filter?: BiquadFilterType
  freq?: number
  /** Filter sweep target; defaults to `freq`. */
  freqEnd?: number
  q?: number
}

function noise(c: AudioContext, spec: NoiseSpec) {
  const t0 = c.currentTime + (spec.at ?? 0)
  const length = Math.ceil(c.sampleRate * (spec.dur + 0.05))
  const buffer = c.createBuffer(1, length, c.sampleRate)
  const data = buffer.getChannelData(0)
  for (let i = 0; i < length; i++) data[i] = Math.random() * 2 - 1
  const src = c.createBufferSource()
  src.buffer = buffer
  const filter = c.createBiquadFilter()
  filter.type = spec.filter ?? 'bandpass'
  filter.frequency.setValueAtTime(spec.freq ?? 1000, t0)
  if (spec.freqEnd !== undefined)
    filter.frequency.exponentialRampToValueAtTime(Math.max(1, spec.freqEnd), t0 + spec.dur)
  filter.Q.value = spec.q ?? 1
  const gain = c.createGain()
  gain.gain.setValueAtTime(0, t0)
  gain.gain.linearRampToValueAtTime(spec.vol ?? 0.3, t0 + 0.005)
  gain.gain.exponentialRampToValueAtTime(0.0005, t0 + spec.dur)
  src.connect(filter).connect(gain).connect(master!)
  src.start(t0)
  src.stop(t0 + spec.dur + 0.05)
}

export function playSfx(name: SfxName): void {
  const c = context()
  if (!c) return

  switch (name) {
    case 'flip': // card swish + snap
      noise(c, { dur: 0.12, filter: 'bandpass', freq: 500, freqEnd: 2400, vol: 0.25, q: 2 })
      tone(c, { at: 0.1, freq: 1800, dur: 0.03, type: 'square', vol: 0.08 })
      break
    case 'shot': // crack + muzzle thump
      noise(c, { dur: 0.22, filter: 'lowpass', freq: 3500, freqEnd: 300, vol: 0.9 })
      tone(c, { freq: 160, freqEnd: 50, dur: 0.25, type: 'sine', vol: 0.6 })
      break
    case 'whoosh': // dodge
      noise(c, { dur: 0.35, filter: 'bandpass', freq: 1600, freqEnd: 250, vol: 0.35, q: 1.5 })
      break
    case 'load': // two mechanical clicks
      tone(c, { freq: 2200, dur: 0.025, type: 'square', vol: 0.15 })
      noise(c, { dur: 0.03, filter: 'highpass', freq: 3000, vol: 0.15 })
      tone(c, { at: 0.11, freq: 1600, dur: 0.03, type: 'square', vol: 0.2 })
      noise(c, { at: 0.11, dur: 0.04, filter: 'highpass', freq: 2500, vol: 0.2 })
      break
    case 'gold': { // chest opens — rising bells
      const notes = [523, 659, 784, 1047]
      notes.forEach((f, i) => {
        tone(c, { at: i * 0.09, freq: f, dur: 0.5, type: 'sine', vol: 0.25 })
        tone(c, { at: i * 0.09, freq: f * 2, dur: 0.3, type: 'triangle', vol: 0.08 })
      })
      break
    }
    case 'standoff': // tense dissonant sting
      tone(c, { freq: 110, dur: 0.6, type: 'sawtooth', vol: 0.2 })
      tone(c, { freq: 117, dur: 0.6, type: 'sawtooth', vol: 0.2 })
      noise(c, { at: 0.05, dur: 0.4, filter: 'lowpass', freq: 500, vol: 0.12 })
      break
    case 'eliminated': // falling sting + thud
      tone(c, { freq: 330, freqEnd: 65, dur: 0.55, type: 'sawtooth', vol: 0.3 })
      tone(c, { at: 0.45, freq: 70, freqEnd: 40, dur: 0.3, type: 'sine', vol: 0.5 })
      noise(c, { at: 0.45, dur: 0.2, filter: 'lowpass', freq: 300, vol: 0.3 })
      break
    case 'cancel': // fizzle
      noise(c, { dur: 0.3, filter: 'bandpass', freq: 900, freqEnd: 200, vol: 0.25, q: 3 })
      tone(c, { freq: 400, freqEnd: 180, dur: 0.25, type: 'triangle', vol: 0.15 })
      break
    case 'fanfare': { // winner ceremony
      const melody: [number, number][] = [
        [523, 0],
        [659, 0.14],
        [784, 0.28],
        [1047, 0.42],
      ]
      for (const [f, at] of melody) tone(c, { at, freq: f, dur: 0.35, type: 'triangle', vol: 0.3 })
      for (const f of [523, 659, 784, 1047])
        tone(c, { at: 0.62, freq: f, dur: 1.2, type: 'triangle', vol: 0.18 })
      for (const f of [262, 330, 392])
        tone(c, { at: 0.62, freq: f, dur: 1.2, type: 'square', vol: 0.05 })
      noise(c, { at: 0.62, dur: 0.5, filter: 'highpass', freq: 5000, vol: 0.08 })
      break
    }
    case 'tick': // countdown urgency
      tone(c, { freq: 1400, dur: 0.04, type: 'square', vol: 0.1 })
      break
  }
}

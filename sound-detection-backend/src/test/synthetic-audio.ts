/** Deterministic synthetic audio for detection tests (48 kHz mono). */

export const RATE = 48_000

export function random(seed: number): () => number {
  let state = seed >>> 0
  return () => {
    state = (state + 0x6d2b79f5) >>> 0
    let value = state
    value = Math.imul(value ^ (value >>> 15), value | 1)
    value ^= value + Math.imul(value ^ (value >>> 7), value | 61)
    return ((value ^ (value >>> 14)) >>> 0) / 4294967296
  }
}

/** Gaussian-ish noise from the sum of uniforms. */
function noiseSample(next: () => number) {
  return (next() + next() + next() + next() - 2) * 0.866
}

/** Room background: low-passed noise plus a mains hum. */
export function background(seconds: number, { seed = 1, level = 0.02, hum = 0.01 } = {}): Float32Array {
  const next = random(seed)
  const samples = new Float32Array(Math.round(seconds * RATE))
  let lowpassed = 0
  for (let index = 0; index < samples.length; index += 1) {
    lowpassed += 0.05 * (noiseSample(next) - lowpassed)
    samples[index] = level * 4 * lowpassed + level * 0.3 * noiseSample(next) +
      hum * Math.sin((2 * Math.PI * 120 * index) / RATE)
  }
  return samples
}

/** A finger snap: a very short, bright, decaying noise burst. */
export function addSnap(samples: Float32Array, atSeconds: number, { seed = 7, amplitude = 0.4 } = {}) {
  const next = random(seed)
  const start = Math.round(atSeconds * RATE)
  let previous = 0
  for (let index = 0; index < 0.03 * RATE && start + index < samples.length; index += 1) {
    const white = noiseSample(next)
    const bright = white - previous
    previous = white
    samples[start + index] += amplitude * bright * Math.exp(-index / (0.006 * RATE))
  }
}

/** A knock: a low, resonant thud of about 150 ms. */
export function addKnock(samples: Float32Array, atSeconds: number, { amplitude = 0.4 } = {}) {
  const start = Math.round(atSeconds * RATE)
  for (let index = 0; index < 0.15 * RATE && start + index < samples.length; index += 1) {
    samples[start + index] += amplitude * Math.sin((2 * Math.PI * 180 * index) / RATE) * Math.exp(-index / (0.04 * RATE))
  }
}

/** A sustained rattle: noise in the 2-4 kHz region, amplitude modulated at 30 Hz. */
export function addRattle(samples: Float32Array, atSeconds: number, seconds: number, { seed = 11, amplitude = 0.15 } = {}) {
  const next = random(seed)
  const start = Math.round(atSeconds * RATE)
  const omega = (2 * Math.PI * 3000) / RATE
  let y1 = 0
  let y2 = 0
  for (let index = 0; index < seconds * RATE && start + index < samples.length; index += 1) {
    // Two-pole resonator around 3 kHz.
    const y = noiseSample(next) + 1.9 * Math.cos(omega) * 0.95 * y1 - 0.9025 * y2
    y2 = y1
    y1 = y
    const modulation = 0.6 + 0.4 * Math.sin((2 * Math.PI * 30 * index) / RATE)
    samples[start + index] += amplitude * 0.05 * y * modulation
  }
}

/** Makes the room itself louder for a moment: a burst with exactly the room's spectral shape. */
export function amplify(samples: Float32Array, atSeconds: number, seconds: number, gain: number) {
  const start = Math.round(atSeconds * RATE)
  const end = Math.min(samples.length, start + Math.round(seconds * RATE))
  for (let index = start; index < end; index += 1) samples[index] *= gain
}

export function scale(samples: Float32Array, gain: number): Float32Array {
  return samples.map((value) => value * gain)
}

/** 16-bit PCM mono WAV, as the server's playback endpoint returns. */
export function encodeWav(samples: Float32Array, sampleRate = RATE): ArrayBuffer {
  const buffer = new ArrayBuffer(44 + samples.length * 2)
  const view = new DataView(buffer)
  const text = (offset: number, value: string) => [...value].forEach((char, index) => view.setUint8(offset + index, char.charCodeAt(0)))
  text(0, "RIFF")
  view.setUint32(4, 36 + samples.length * 2, true)
  text(8, "WAVE")
  text(12, "fmt ")
  view.setUint32(16, 16, true)
  view.setUint16(20, 1, true)
  view.setUint16(22, 1, true)
  view.setUint32(24, sampleRate, true)
  view.setUint32(28, sampleRate * 2, true)
  view.setUint16(32, 2, true)
  view.setUint16(34, 16, true)
  text(36, "data")
  view.setUint32(40, samples.length * 2, true)
  samples.forEach((value, index) => view.setInt16(44 + index * 2, Math.max(-32768, Math.min(32767, Math.round(value * 32767))), true))
  return buffer
}

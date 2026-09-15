import { describe, expect, it } from "vitest"

import {
  BAND_COUNT,
  FLOOR_DB,
  FrameAnalyzer,
  MATCH_FRAME_SIZE,
  MATCH_HOP_SIZE,
  MATCH_SAMPLE_RATE,
  Radix2Fft,
  bandCentersHz,
  extractAcousticFeatures,
  frameCountFor,
  frameSpanMs,
} from "./acoustic-features"

function tone(hz: number, amplitude = 0.5): Float32Array {
  return Float32Array.from({ length: MATCH_FRAME_SIZE },
    (_, index) => amplitude * Math.sin((2 * Math.PI * hz * index) / MATCH_SAMPLE_RATE))
}

const loudestBand = (bands: readonly number[]) => bands.indexOf(Math.max(...bands))

describe("acoustic feature extraction", () => {
  it("produces 40 finite frequency-band values", () => {
    const features = extractAcousticFeatures(tone(1000), MATCH_SAMPLE_RATE)
    expect(features).not.toBeNull()
    expect(features!.bands).toHaveLength(BAND_COUNT)
    expect(features!.bands.every(Number.isFinite)).toBe(true)
    expect(features!.rmsDb).toBeGreaterThan(-9.2)
    expect(features!.rmsDb).toBeLessThan(-8.8)
  })

  it("puts a tone's energy in the band around its frequency", () => {
    for (const hz of [300, 1000, 6000]) {
      const band = loudestBand(extractAcousticFeatures(tone(hz), MATCH_SAMPLE_RATE)!.bands)
      expect(bandCentersHz[band] / hz).toBeGreaterThan(0.8)
      expect(bandCentersHz[band] / hz).toBeLessThan(1.25)
    }
  })

  it("rejects silence and invalid samples", () => {
    expect(extractAcousticFeatures(new Float32Array(MATCH_FRAME_SIZE), MATCH_SAMPLE_RATE)).toBeNull()
    const samples = tone(1000)
    samples[10] = Number.NaN
    expect(extractAcousticFeatures(samples, MATCH_SAMPLE_RATE)).toBeNull()
  })

  it("requires the agreed sample rate and a complete frame", () => {
    expect(() => extractAcousticFeatures(tone(1000), 44_100)).toThrow(RangeError)
    expect(() => extractAcousticFeatures(new Float32Array(100), MATCH_SAMPLE_RATE)).toThrow(RangeError)
  })
})

describe("FrameAnalyzer", () => {
  it("reports digital silence at the floor and invalid samples as NaN", () => {
    const analyzer = new FrameAnalyzer()
    const bands = new Float32Array(BAND_COUNT).fill(5)
    expect(analyzer.analyze(new Float32Array(MATCH_FRAME_SIZE), 0, bands)).toBe(FLOOR_DB)
    expect([...bands].every((value) => value === FLOOR_DB)).toBe(true)
    const invalid = tone(1000)
    invalid[3] = Number.POSITIVE_INFINITY
    bands.fill(7)
    expect(analyzer.analyze(invalid, 0, bands)).toBeNaN()
    expect(bands[0]).toBe(7)
  })

  it("analyses a frame at an offset inside a longer clip", () => {
    const clip = new Float32Array(MATCH_FRAME_SIZE * 2)
    clip.set(tone(2000), MATCH_FRAME_SIZE)
    const analyzer = new FrameAnalyzer()
    const bands = new Float32Array(BAND_COUNT)
    expect(analyzer.analyze(clip, 0, bands)).toBe(FLOOR_DB)
    expect(analyzer.analyze(clip, MATCH_FRAME_SIZE, bands)).toBeCloseTo(-9.03, 1)
    expect(() => analyzer.analyze(clip, MATCH_FRAME_SIZE + 1, bands)).toThrow(RangeError)
  })
})

describe("Radix2Fft", () => {
  it("matches a direct DFT", () => {
    const size = 32
    const input = Array.from({ length: size }, (_, index) => Math.sin(index * 0.7) + (index % 5) / 5)
    const real = Float64Array.from(input)
    const imaginary = new Float64Array(size)
    new Radix2Fft(size).transform(real, imaginary)
    for (let bin = 0; bin < size; bin += 1) {
      let expectedReal = 0
      let expectedImaginary = 0
      input.forEach((value, index) => {
        expectedReal += value * Math.cos((2 * Math.PI * bin * index) / size)
        expectedImaginary -= value * Math.sin((2 * Math.PI * bin * index) / size)
      })
      expect(real[bin]).toBeCloseTo(expectedReal, 9)
      expect(imaginary[bin]).toBeCloseTo(expectedImaginary, 9)
    }
  })

  it("rejects sizes that are not powers of two", () => {
    expect(() => new Radix2Fft(48)).toThrow(RangeError)
    expect(() => new Radix2Fft(1)).toThrow(RangeError)
  })
})

describe("frame timing", () => {
  it("counts whole frames at the live hop and reports their span", () => {
    expect(frameCountFor(MATCH_FRAME_SIZE - 1)).toBe(0)
    expect(frameCountFor(MATCH_FRAME_SIZE)).toBe(1)
    expect(frameCountFor(MATCH_FRAME_SIZE + MATCH_HOP_SIZE)).toBe(2)
    const span = frameSpanMs(1)
    expect(span.startMs).toBeCloseTo((MATCH_HOP_SIZE * 1000) / MATCH_SAMPLE_RATE)
    expect(span.endMs - span.startMs).toBeCloseTo((MATCH_FRAME_SIZE * 1000) / MATCH_SAMPLE_RATE)
  })
})

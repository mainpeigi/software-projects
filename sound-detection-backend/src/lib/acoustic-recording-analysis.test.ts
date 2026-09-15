import { describe, expect, it, vi } from "vitest"

import { BAND_COUNT, frameCountFor } from "./acoustic-features"
import {
  DISPLAY_ROWS,
  ENVELOPE_BLOCK,
  analyzeRecording,
  analyzeRecordingSync,
  backgroundFrames,
  detectOnsets,
  frameStartMs,
  nearestOnset,
  rangeProfile,
  recordingBackground,
  resolveKind,
  segmentFrames,
  FRAME_MS,
} from "./acoustic-recording-analysis"
import { amplify, background } from "@/test/synthetic-audio"
import { snapRecording } from "@/test/acoustic-fixtures"

const snaps = snapRecording()
const analysis = analyzeRecordingSync(snaps.samples)

describe("recording analysis", () => {
  it("covers the whole clip with frames, a spectrogram and a waveform envelope", () => {
    expect(analysis.frameCount).toBe(frameCountFor(snaps.samples.length))
    expect(analysis.durationMs).toBeCloseTo(3000)
    expect(analysis.bands).toHaveLength(analysis.frameCount * BAND_COUNT)
    expect(analysis.spectrogram).toHaveLength(analysis.frameCount * DISPLAY_ROWS)
    expect(analysis.envelope).toHaveLength(Math.ceil(snaps.samples.length / ENVELOPE_BLOCK) * 2)
    const snapBlock = Math.floor((0.8 * 48_000) / ENVELOPE_BLOCK)
    expect(analysis.envelope[snapBlock * 2 + 1]).toBeGreaterThan(0.1)
  })

  it("skips display data when only features are needed", () => {
    const lean = analyzeRecordingSync(snaps.samples, { display: false })
    expect(lean.spectrogram).toHaveLength(0)
    expect(lean.envelope).toHaveLength(0)
    expect(lean.bands).toEqual(analysis.bands)
  })

  it("yields the same frames asynchronously, reports progress and can be cancelled", async () => {
    const onProgress = vi.fn()
    const result = await analyzeRecording(snaps.samples, { onProgress })
    expect(result.bands).toEqual(analysis.bands)
    expect(onProgress).toHaveBeenLastCalledWith(analysis.frameCount, analysis.frameCount)
    const controller = new AbortController()
    controller.abort()
    await expect(analyzeRecording(snaps.samples, { signal: controller.signal })).rejects.toMatchObject({ name: "AbortError" })
  })
})

describe("onsets", () => {
  it("finds each snap within a few milliseconds", () => {
    const onsets = detectOnsets(analysis)
    expect(onsets).toHaveLength(2)
    expect(onsets[0].startMs).toBeGreaterThan(760)
    expect(onsets[0].startMs).toBeLessThan(805)
    expect(onsets[0].endMs).toBeGreaterThan(820)
    expect(onsets[0].endMs).toBeLessThan(880)
    expect(onsets[1].peakMs).toBeGreaterThan(1880)
    expect(onsets[1].peakMs).toBeLessThan(1950)
    expect(onsets[0].peakLevelDb).toBeGreaterThan(15)
  })

  it("finds a burst with the room's own spectral shape", () => {
    const samples = background(3, { seed: 73 })
    amplify(samples, 1.5, 0.08, 6)
    const onsets = detectOnsets(analyzeRecordingSync(samples, { display: false }))
    expect(onsets).toHaveLength(1)
    expect(onsets[0].peakMs).toBeGreaterThan(1450)
    expect(onsets[0].peakMs).toBeLessThan(1650)
  })

  it("finds nothing in plain room noise", () => {
    expect(detectOnsets(analyzeRecordingSync(background(3, { seed: 2 }), { display: false }))).toEqual([])
  })

  it("picks the onset nearest to a time within reach", () => {
    const onsets = detectOnsets(analysis)
    expect(nearestOnset(onsets, 1000)).toBe(onsets[0])
    expect(nearestOnset(onsets, 1870)).toBe(onsets[1])
    expect(nearestOnset(onsets, 2900, 200)).toBeNull()
  })
})

describe("segments and background", () => {
  it("assigns a short segment to the frames that mostly overlap it", () => {
    const frames = segmentFrames(analysis, { startMs: 800, endMs: 840 })
    expect(frames.length).toBeGreaterThanOrEqual(3)
    for (const frame of frames) {
      const start = frameStartMs(frame)
      expect(Math.min(start + FRAME_MS, 840) - Math.max(start, 800)).toBeGreaterThanOrEqual(19.99)
    }
    expect(segmentFrames(analysis, { startMs: 5000, endMs: 5100 })).toEqual([])
  })

  it("keeps background frames and their context clear of segments", () => {
    const frames = new Set(backgroundFrames(analysis, [{ startMs: 800, endMs: 840 }]))
    for (const frame of segmentFrames(analysis, { startMs: 800, endMs: 840 })) expect(frames.has(frame)).toBe(false)
    expect(frames.has(0)).toBe(true)
    expect(frames.has(analysis.frameCount - 1)).toBe(true)
  })

  it("learns the room, not the snaps", () => {
    const plain = recordingBackground(analyzeRecordingSync(background(3, { seed: 1 }), { display: false }), [])
    const learned = recordingBackground(analysis, snaps.segments)
    const difference = learned.level.reduce((total, value, band) => total + Math.abs(value - plain.level[band]), 0) / BAND_COUNT
    expect(difference).toBeLessThan(1)
  })

  it("profiles how far a moment rises above the background", () => {
    const room = recordingBackground(analysis, snaps.segments)
    const snap = rangeProfile(analysis, { startMs: 795, endMs: 835 }, room)!
    expect(snap.peakLevelDb).toBeGreaterThan(15)
    expect(snap.difference[BAND_COUNT - 5]).toBeGreaterThan(snap.allowance[BAND_COUNT - 5])
    const quiet = rangeProfile(analysis, { startMs: 2400, endMs: 2600 }, room)!
    expect(quiet.peakLevelDb).toBeLessThan(3)
    expect(quiet.frames).toBeGreaterThan(10)
  })

  it("resolves Auto segments by length", () => {
    expect(resolveKind({ startMs: 0, endMs: 40, kind: "Auto" })).toBe("Impulsive")
    expect(resolveKind({ startMs: 0, endMs: 400, kind: "Auto" })).toBe("Sustained")
    expect(resolveKind({ startMs: 0, endMs: 40, kind: "Sustained" })).toBe("Sustained")
  })
})

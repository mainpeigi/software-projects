import { describe, expect, it } from "vitest"

import { DEFAULT_DETECTOR_SETTINGS } from "./acoustic-detector"
import {
  SWEEP_THRESHOLDS,
  evaluable,
  replayAlarms,
  scoreRecording,
  summarize,
  sweepThresholds,
  type Alarm,
  type TruthRecording,
} from "./acoustic-evaluation"
import { ReferenceMatcher } from "./acoustic-matching"
import { addSnap, background } from "@/test/synthetic-audio"
import { referencesFor, snapRecording } from "@/test/acoustic-fixtures"

const references = referencesFor([snapRecording("first"), snapRecording("second")])
const matcher = new ReferenceMatcher(references)
const alarm = (startMs: number, endMs: number): Alarm => ({
  startMs, endMs, match: { recordingId: "first", segmentId: null, kind: "Impulsive", similarity: 0.9, margin: 0.9, referenceIndex: 0 },
})
const truth = (overrides: Partial<TruthRecording> = {}): TruthRecording => ({
  recordingId: "take", label: "Anomaly", annotated: true, durationMs: 60_000, segments: [{ startMs: 1000, endMs: 1040 }], ...overrides,
})

describe("offline scoring", () => {
  const take = background(4, { seed: 61 })
  addSnap(take, 1.2, { seed: 62 })
  addSnap(take, 2.8, { seed: 63 })
  const scored = scoreRecording(matcher, DEFAULT_DETECTOR_SETTINGS, take, { recordingId: "take" })

  it("finds each snap and nothing else", () => {
    expect(scored.track.frameCount).toBeGreaterThan(300)
    expect(scored.alarms).toHaveLength(2)
    expect(scored.alarms[0].startMs).toBeGreaterThan(1150)
    expect(scored.alarms[0].startMs).toBeLessThan(1300)
    const summary = summarize([{ truth: truth({ durationMs: 4000, segments: [{ startMs: 1200, endMs: 1230 }, { startMs: 2800, endMs: 2830 }] }), alarms: scored.alarms }])
    expect(summary).toMatchObject({ occurrences: 2, detected: 2, recall: 1, alarms: 2, falseAlarms: 0, precision: 1 })
  })

  it("replays the same decisions from stored scores and honours a stricter threshold", () => {
    const replayed = replayAlarms(scored.track, references, DEFAULT_DETECTOR_SETTINGS)
    expect(replayed.map((item) => Math.round(item.startMs))).toEqual(scored.alarms.map((item) => Math.round(item.startMs)))
    expect(replayAlarms(scored.track, references, { ...DEFAULT_DETECTOR_SETTINGS, threshold: 1 }).length).toBeLessThanOrEqual(2)
  })

  it("leaves a recording's own patterns out when asked", () => {
    const own = snapRecording("first")
    const self = scoreRecording(matcher, DEFAULT_DETECTOR_SETTINGS, own.samples, { recordingId: "first", excludeRecordingId: "first" })
    expect(self.alarms).toHaveLength(2)
    expect(Array.from(self.track.anomalyIndex).every((row) => row < 0 || references.sources[row].recordingId === "second")).toBe(true)
  })
})

describe("summaries", () => {
  it("matches alarms to occurrences with a tolerance and counts the rest as false alarms", () => {
    const summary = summarize([
      { truth: truth(), alarms: [alarm(1200, 1250), alarm(5000, 5010)] },
      { truth: truth({ recordingId: "room", label: "Normal", annotated: false, segments: [] }), alarms: [alarm(100, 110)] },
    ])
    expect(summary).toMatchObject({ recordings: 2, occurrences: 1, detected: 1, alarms: 3, falseAlarms: 2 })
    expect(summary.falseAlarmsPerMinute).toBeCloseTo(1)
    expect(summary.precision).toBeCloseTo(1 / 3)
  })

  it("skips Anomaly recordings without ground truth", () => {
    const raw = truth({ annotated: false })
    expect(evaluable(raw)).toBe(false)
    expect(summarize([{ truth: raw, alarms: [alarm(0, 10)] }])).toMatchObject({ recordings: 0, alarms: 0, recall: null, falseAlarmsPerMinute: null })
  })

  it("sweeps thresholds with recall that never rises as the threshold rises", () => {
    const take = background(3, { seed: 64 })
    addSnap(take, 1, { seed: 65 })
    const { track } = scoreRecording(matcher, DEFAULT_DETECTOR_SETTINGS, take, { recordingId: "take" })
    const rows = sweepThresholds([{ truth: truth({ durationMs: 3000, segments: [{ startMs: 1000, endMs: 1030 }] }), track }], references, DEFAULT_DETECTOR_SETTINGS)
    expect(rows.map((row) => row.threshold)).toEqual(SWEEP_THRESHOLDS)
    const recalls = rows.map((row) => row.summary.recall ?? 0)
    recalls.slice(1).forEach((value, index) => expect(value).toBeLessThanOrEqual(recalls[index]))
    expect(recalls[0]).toBe(1)
  })
})

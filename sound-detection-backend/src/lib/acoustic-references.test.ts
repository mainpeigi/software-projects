import { describe, expect, it } from "vitest"

import { BAND_COUNT } from "./acoustic-features"
import { analyzeRecordingSync } from "./acoustic-recording-analysis"
import { backgroundWithout, buildReferenceSet, extractRecordingReferences, referenceExcess } from "./acoustic-references"
import { addKnock, background } from "@/test/synthetic-audio"
import { referencesFor, snapRecording } from "@/test/acoustic-fixtures"

const analyse = (samples: Float32Array) => analyzeRecordingSync(samples, { display: false })

function knockRecording() {
  const samples = background(3, { seed: 8 })
  addKnock(samples, 1.2)
  return samples
}

describe("reference extraction", () => {
  it("learns the anomaly only from its segments", () => {
    const recording = snapRecording()
    const part = extractRecordingReferences(analyse(recording.samples), recording, recording.segments)
    const anomaly = part.sources.filter((source) => source.label === "Anomaly")
    expect(anomaly.length).toBeGreaterThanOrEqual(4)
    expect(new Set(anomaly.map((source) => source.segmentId))).toEqual(new Set(["snaps-1", "snaps-2"]))
    expect(anomaly.every((source) => source.kind === "Impulsive")).toBe(true)
    expect(anomaly.every((source) => (source.timeMs > 750 && source.timeMs < 900) || (source.timeMs > 1850 && source.timeMs < 2000))).toBe(true)
    expect(part.automatic).toBe(false)
    expect(part.silentSegments).toBe(0)
  })

  it("does not turn room noise into patterns, and flags a segment that does not stand out", () => {
    const recording = snapRecording()
    const part = extractRecordingReferences(analyse(recording.samples), recording,
      [...recording.segments, { id: "empty", startMs: 2400, endMs: 2600, kind: "Auto" }])
    expect(part.sources.some((source) => source.segmentId === "empty")).toBe(false)
    expect(part.silentSegments).toBe(1)
    expect(part.sources.filter((source) => source.implicit)).toHaveLength(0)
  })

  it("keeps other loud sounds of an Anomaly recording as implicit normal patterns", () => {
    const recording = snapRecording()
    addKnock(recording.samples, 2.4)
    const part = extractRecordingReferences(analyse(recording.samples), recording, recording.segments)
    const implicit = part.sources.filter((source) => source.implicit)
    expect(implicit.length).toBeGreaterThan(0)
    expect(implicit.every((source) => source.label === "Normal" && source.timeMs > 2350 && source.timeMs < 2700)).toBe(true)
  })

  it("falls back to detected onsets for an unannotated Anomaly recording", () => {
    const recording = snapRecording("raw", { annotated: false })
    const part = extractRecordingReferences(analyse(recording.samples), recording, [])
    expect(part.automatic).toBe(true)
    expect(part.segments).toHaveLength(2)
    expect(part.sources.filter((source) => source.label === "Anomaly").every((source) => source.segmentId === null)).toBe(true)
  })

  it("uses Normal recordings whole", () => {
    const part = extractRecordingReferences(analyse(knockRecording()), { id: "knocks", label: "Normal" },
      [{ id: "ignored", startMs: 0, endMs: 100, kind: "Auto" }])
    expect(part.segments).toEqual([])
    expect(part.sources.length).toBeGreaterThan(0)
    expect(part.sources.every((source) => source.label === "Normal" && !source.implicit)).toBe(true)
  })
})

describe("reference sets", () => {
  const first = snapRecording("first")
  const second = snapRecording("second")

  it("numbers occurrences across recordings and keeps each recording's background", () => {
    const references = referencesFor([first, second])
    const groups = new Set(references.sources.filter((source) => source.label === "Anomaly").map((source) => source.group))
    expect([...groups].sort()).toEqual([0, 1, 2, 3])
    expect(Object.keys(references.recordingBackgrounds).sort()).toEqual(["first", "second"])
    expect(backgroundWithout(references, "first")).toEqual(references.recordingBackgrounds.second)
    expect(backgroundWithout(references, null)).toBe(references.background)
  })

  it("thins normal patterns before ever dropping anomaly patterns", () => {
    const knocks = { id: "knocks", label: "Normal" as const, samples: knockRecording(), segments: [] }
    const full = referencesFor([first, knocks])
    const anomalies = full.sources.filter((source) => source.label === "Anomaly").length
    const limited = referencesFor([first, knocks], anomalies + 2)
    expect(limited.sources.filter((source) => source.label === "Anomaly")).toHaveLength(anomalies)
    expect(limited.sources.filter((source) => source.label === "Normal").length).toBeLessThanOrEqual(2)
    expect(() => referencesFor([first, knocks], anomalies - 1)).toThrow("too many sound patterns")
  })

  it("restores a pattern's newest frame in dB above background", () => {
    const references = referencesFor([first])
    const excess = referenceExcess(references, 0)
    expect(excess).toHaveLength(BAND_COUNT)
    expect(Math.max(...excess)).toBeGreaterThan(5)
    expect(buildReferenceSet([]).sources).toEqual([])
  })
})

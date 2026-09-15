import { describe, expect, it } from "vitest"

import { calibrate } from "./acoustic-calibration"
import { addKnock, addSnap, background } from "@/test/synthetic-audio"
import { referencesFor, snapRecording } from "@/test/acoustic-fixtures"

function knocks() {
  const samples = background(3, { seed: 8 })
  addKnock(samples, 0.7)
  addKnock(samples, 2.1)
  return { id: "knocks", label: "Normal" as const, samples, segments: [] }
}

describe("calibration", () => {
  it("suggests a threshold that separates normal sounds from recognisable snaps", () => {
    const result = calibrate(referencesFor([snapRecording("first"), snapRecording("second"), knocks()]))
    expect(result.normalPatterns).toBeGreaterThan(0)
    expect(result.occurrences).toBe(4)
    expect(result.suggestedThreshold).toBeGreaterThanOrEqual(0.6)
    expect(result.suggestedThreshold).toBeLessThanOrEqual(0.98)
    expect(result.recognisedAtSuggestion).toBe(1)
    expect(result.separable).toBe(true)
  })

  it("explains what cannot be measured", () => {
    const samples = background(3, { seed: 1 })
    addSnap(samples, 0.8, { seed: 3 })
    const result = calibrate(referencesFor([{ id: "single", label: "Anomaly", samples, segments: [{ id: "s", startMs: 795, endMs: 835, kind: "Auto" }] }]))
    expect(result.suggestedThreshold).toBeNull()
    expect(result.separable).toBeNull()
    expect(result.notes.join(" ")).toMatch(/Nothing outside the anomaly stands out/)
    expect(result.notes.join(" ")).toMatch(/at least two occurrences/)
  })

  it("points out an occurrence that was left unannotated", () => {
    const recording = snapRecording()
    const result = calibrate(referencesFor([{ ...recording, segments: recording.segments.slice(0, 1) }]))
    expect(result.suggestedThreshold).toBeGreaterThanOrEqual(0.9)
    expect(result.notes.join(" ")).toMatch(/unannotated occurrences/)
  })

  it("warns when normal sounds look like the anomaly", () => {
    const result = calibrate(referencesFor([snapRecording("first"), { ...snapRecording("normal"), label: "Normal", segments: [] }]))
    expect(result.suggestedThreshold).toBeGreaterThanOrEqual(0.9)
  })
})

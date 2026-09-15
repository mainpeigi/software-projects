import { describe, expect, it } from "vitest"

import { AcousticDetector, DEFAULT_DETECTOR_SETTINGS } from "./acoustic-detector"
import { ReferenceMatcher } from "./acoustic-matching"
import { addKnock, addRattle, addSnap, amplify, background, scale } from "@/test/synthetic-audio"
import { collectAlarms, rattleRecording, referencesFor, snapRecording } from "@/test/acoustic-fixtures"

const detectorFor = (recordings: Parameters<typeof referencesFor>[0], settings = DEFAULT_DETECTOR_SETTINGS) =>
  new AcousticDetector(new ReferenceMatcher(referencesFor(recordings)), settings)

describe("AcousticDetector", () => {
  it("raises one event per snap and ignores room noise, a knock and a rattle", () => {
    const live = background(5, { seed: 99 })
    addSnap(live, 1.0, { seed: 21 })
    addKnock(live, 2.0)
    addSnap(live, 3.0, { seed: 22 })
    addRattle(live, 3.6, 1)
    const alarms = collectAlarms(detectorFor([snapRecording()]), live)
    expect(alarms.map((alarm) => alarm.kind)).toEqual(["Impulsive", "Impulsive"])
    expect(alarms[0].start).toBeGreaterThan(0.99)
    expect(alarms[0].start).toBeLessThan(1.1)
    expect(alarms[1].start).toBeGreaterThan(2.99)
    expect(alarms[1].start).toBeLessThan(3.1)
  })

  it("never alarms on background noise, even when the recording was not annotated", () => {
    const room = background(6, { seed: 42 })
    expect(collectAlarms(detectorFor([snapRecording()]), room)).toEqual([])
    expect(collectAlarms(detectorFor([snapRecording("raw", { annotated: false })]), room)).toEqual([])
  })

  it("keeps a sustained anomaly detected instead of learning it as background", () => {
    const live = background(8, { seed: 77 })
    addRattle(live, 1, 6.5, { seed: 31 })
    const alarms = collectAlarms(detectorFor([rattleRecording()]), live)
    expect(alarms).toHaveLength(1)
    expect(alarms[0].kind).toBe("Sustained")
    expect(alarms[0].start).toBeLessThan(1.4)
    expect(alarms[0].end).toBeGreaterThan(7)
  })

  it("adapts to a louder room and still hears the snap", () => {
    const live = scale(background(7, { seed: 55 }), 2)
    addSnap(live, 5.0, { seed: 23 })
    const detector = detectorFor([snapRecording()])
    const initial = Float32Array.from(detector.background.level)
    const alarms = collectAlarms(detector, live)
    expect(alarms).toHaveLength(1)
    expect(alarms[0].start).toBeGreaterThan(4.99)
    const risen = detector.background.level.reduce((total, value, band) => total + value - initial[band], 0) / initial.length
    expect(risen).toBeGreaterThan(4)
    const learned = Float32Array.from(detector.background.level)
    detector.configure({ ...DEFAULT_DETECTOR_SETTINGS, threshold: 0.9 })
    expect(Float32Array.from(detector.background.level)).toEqual(learned)
  })

  it("hears a burst with the room's own spectral shape", () => {
    const recording = background(3, { seed: 71 })
    amplify(recording, 1, 0.1, 6)
    const live = background(4, { seed: 72 })
    amplify(live, 2, 0.1, 6)
    const detector = detectorFor([{ id: "burst", label: "Anomaly", samples: recording, segments: [{ id: "b", startMs: 1000, endMs: 1100, kind: "Auto" }] }])
    const alarms = collectAlarms(detector, live)
    expect(alarms).toHaveLength(1)
    expect(alarms[0].start).toBeGreaterThan(1.99)
    expect(alarms[0].start).toBeLessThan(2.15)
    expect(Math.abs(detector.gainOffsetDb)).toBeLessThan(1)
  })

  it("lets an equally good normal example suppress the match", () => {
    const normal = snapRecording("normal-snaps")
    const live = background(3, { seed: 12 })
    addSnap(live, 1.5, { seed: 24 })
    const alarms = collectAlarms(detectorFor([snapRecording(), { ...normal, label: "Normal", segments: [] }]), live)
    expect(alarms).toEqual([])
  })

  it("reports unknown loud sounds only when asked to", () => {
    const live = background(4, { seed: 13 })
    addRattle(live, 1, 1.5, { seed: 32 })
    expect(collectAlarms(detectorFor([snapRecording()]), live)).toEqual([])
    const alarms = collectAlarms(detectorFor([snapRecording()], { ...DEFAULT_DETECTOR_SETTINGS, unknownBelow: 0.6 }), live)
    expect(alarms.map((alarm) => alarm.kind)).toEqual(["Unknown"])
  })

  it("resets its context and gate on invalid frames", () => {
    const detector = detectorFor([snapRecording()])
    const result = detector.processBands(new Float32Array(40), Number.NaN, 1)
    expect(result.decision).toBeNull()
    expect(result.gate.anomaly).toBe(false)
  })
})

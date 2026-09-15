import { describe, expect, it } from "vitest"

import { AcousticMatchGate } from "./acoustic-match-gate"
import type { AcousticMatch } from "./acoustic-matching"

const match = (kind: AcousticMatch["kind"], similarity = 0.9, recordingId = "reference"): AcousticMatch => ({
  recordingId, segmentId: null, kind, similarity, margin: similarity, referenceIndex: 0,
})
const snap = match("Impulsive")
const rattle = match("Sustained")

describe("AcousticMatchGate", () => {
  it("opens an impulsive event on one frame, holds it, then enforces a refractory pause", () => {
    const gate = new AcousticMatchGate()
    expect(gate.update(snap, 1)).toEqual({ anomaly: true, match: snap, eventId: 1, startedAt: 1 })
    expect(gate.update(null, 1.2).anomaly).toBe(true)
    expect(gate.update(null, 1.49).anomaly).toBe(true)
    expect(gate.update(null, 1.5)).toEqual({ anomaly: false, match: null, eventId: 0, startedAt: null })
    expect(gate.update(snap, 1.6).anomaly).toBe(false)
    expect(gate.update(snap, 1.81)).toMatchObject({ anomaly: true, eventId: 2 })
  })

  it("needs 200 ms of sustained evidence and tolerates short dropouts", () => {
    const gate = new AcousticMatchGate()
    expect(gate.update(rattle, 0).anomaly).toBe(false)
    expect(gate.engaged).toBe(true)
    expect(gate.update(null, 0.04).anomaly).toBe(false)
    expect(gate.update(rattle, 0.08).anomaly).toBe(false)
    expect(gate.update(rattle, 0.2)).toMatchObject({ anomaly: true, startedAt: 0, eventId: 1 })
  })

  it("restarts the attack after a longer dropout", () => {
    const gate = new AcousticMatchGate()
    gate.update(rattle, 0)
    gate.update(null, 0.06)
    expect(gate.engaged).toBe(false)
    gate.update(rattle, 0.1)
    expect(gate.update(rattle, 0.2).anomaly).toBe(false)
    expect(gate.update(rattle, 0.3).anomaly).toBe(true)
  })

  it("releases a sustained event half a second after its last match", () => {
    const gate = new AcousticMatchGate()
    gate.update(rattle, 0)
    gate.update(rattle, 0.1)
    gate.update(rattle, 0.2)
    expect(gate.update(null, 0.4).anomaly).toBe(true)
    expect(gate.update(null, 0.69).anomaly).toBe(true)
    expect(gate.update(null, 0.7).anomaly).toBe(false)
  })

  it("reports the strongest match of the event", () => {
    const gate = new AcousticMatchGate()
    gate.update(match("Sustained", 0.85, "a"), 0)
    gate.update(match("Sustained", 0.95, "b"), 0.1)
    expect(gate.update(match("Sustained", 0.9, "c"), 0.2).match?.recordingId).toBe("b")
    expect(gate.update(match("Sustained", 0.97, "d"), 0.3).match?.recordingId).toBe("d")
  })

  it.each([0.61, -1])("resets on discontinuous time %s", (time) => {
    const gate = new AcousticMatchGate()
    gate.update(rattle, 0.1)
    gate.update(rattle, 0.3)
    expect(gate.update(rattle, time).anomaly).toBe(false)
    expect(gate.update(rattle, time + 0.1).anomaly).toBe(false)
    expect(gate.update(rattle, time + 0.2).anomaly).toBe(true)
  })

  it("does not accumulate evidence from duplicate timestamps and resets on invalid time", () => {
    const gate = new AcousticMatchGate()
    gate.update(rattle, 1)
    expect(gate.update(rattle, 1).anomaly).toBe(false)
    gate.update(rattle, 1.1)
    expect(gate.update(rattle, Number.NaN)).toMatchObject({ anomaly: false })
    expect(gate.engaged).toBe(false)
  })
})

import { describe, expect, it } from "vitest"

import { BAND_COUNT, VECTOR_LENGTH } from "./acoustic-features"
import { createBackground } from "./acoustic-background"
import {
  DEFAULT_MATCH_SETTINGS,
  EXCESS_CAP_DB,
  ExcessHistory,
  ReferenceMatcher,
  decide,
  emptyScore,
  commonShift,
  frameExcess,
  matchFromScore,
  normalizeInPlace,
  validateMatchSettings,
  type ReferenceSet,
  type ReferenceSource,
} from "./acoustic-matching"

const quiet = createBackground(new Array(BAND_COUNT).fill(0), new Array(BAND_COUNT).fill(1))

function unit(values: Record<number, number>): number[] {
  const vector = new Float32Array(VECTOR_LENGTH)
  for (const [index, value] of Object.entries(values)) vector[Number(index)] = value
  normalizeInPlace(vector)
  return Array.from(vector)
}

function set(rows: { vector: number[]; source: Partial<ReferenceSource> }[]): ReferenceSet {
  return {
    vectors: Float32Array.from(rows.flatMap((row) => row.vector)),
    levels: Float32Array.from(rows, () => 10),
    norms: Float32Array.from(rows, () => 1),
    sources: rows.map(({ source }, index) => ({
      recordingId: "recording", segmentId: null, label: "Anomaly", kind: "Impulsive", implicit: false,
      timeMs: index * 10, group: 0, ...source,
    })),
    background: quiet,
    recordingBackgrounds: {},
  }
}

describe("frameExcess", () => {
  it("counts only what rises beyond the allowed fluctuation", () => {
    const bands = new Array(BAND_COUNT).fill(0)
    bands[5] = 12
    const out = new Float32Array(BAND_COUNT)
    expect(frameExcess(bands, quiet, out)).toBeCloseTo(10 / 4)
    expect(out[5]).toBeCloseTo(10)
    expect(out[4]).toBe(0)
    bands.fill(10, 0, 4)
    expect(frameExcess(bands, quiet, out)).toBeCloseTo((10 + 8 + 8 + 8) / 4)
  })

  it("measures a level change shared by most bands and discounts a given gain offset", () => {
    const out = new Float32Array(BAND_COUNT)
    const louder = new Array(BAND_COUNT).fill(6)
    louder[7] = 20
    expect(commonShift(louder, quiet)).toBe(6)
    expect(commonShift(new Array(BAND_COUNT).fill(-6), quiet)).toBe(-6)
    expect(commonShift(new Array(BAND_COUNT).fill(0).fill(9, 0, 20), quiet)).toBe(0)
    frameExcess(louder, quiet, out, 0, 6)
    expect(out[7]).toBeCloseTo(20 - 6 - 2)
    expect(out[0]).toBe(0)
  })

  it("treats quieter, invalid and extreme bands safely", () => {
    const bands = new Array(BAND_COUNT).fill(-30)
    bands[0] = Number.NaN
    bands[1] = 500
    const out = new Float32Array(BAND_COUNT)
    frameExcess(bands, quiet, out)
    expect(out[0]).toBe(0)
    expect(out[1]).toBe(EXCESS_CAP_DB)
    expect(out[2]).toBe(0)
  })
})

describe("ExcessHistory", () => {
  it("locks onto a gain offset at once, then follows slowly so a sudden broadband burst stands out", () => {
    const history = new ExcessHistory()
    const offset = new Array(BAND_COUNT).fill(6)
    expect(history.push(offset, quiet)).toBe(0)
    expect(history.gainDb).toBe(6)
    const burst = new Array(BAND_COUNT).fill(16)
    expect(history.push(burst, quiet, { dtSeconds: 0.01 })).toBeCloseTo(16 - 6.1 - 2)
    history.push(burst, quiet, { dtSeconds: 1, frozen: true })
    expect(history.gainDb).toBeCloseTo(6.1)
    for (let step = 0; step < 20; step += 1) history.push(burst, quiet, { dtSeconds: 0.1 })
    expect(history.gainDb).toBeCloseTo(16)
    history.clear()
    expect(history.gainDb).toBeCloseTo(16)
  })

  it("stacks oldest first and keeps the loudest level of the stack", () => {
    const history = new ExcessHistory()
    // A sound in part of the spectrum; a flat rise of every band would read as a gain change.
    const loud = new Array(BAND_COUNT).fill(0).fill(22, 0, 10)
    const silent = new Array(BAND_COUNT).fill(0)
    expect(history.push(loud, quiet)).toBeCloseTo(20)
    expect(history.push(silent, quiet)).toBeCloseTo(20)
    expect(history.push(silent, quiet)).toBeCloseTo(20)
    const vector = new Float32Array(VECTOR_LENGTH)
    expect(history.vector(vector)).toBeGreaterThan(0)
    expect(vector[0]).toBeGreaterThan(0)
    expect(vector[VECTOR_LENGTH - 1]).toBe(0)
    expect(history.push(silent, quiet)).toBe(0)
    expect(history.vector(vector)).toBe(0)
  })
})

describe("ReferenceMatcher", () => {
  const snap = unit({ 100: 1, 101: 1, 102: 1 })
  const hum = unit({ 80: 1, 81: 1 })

  it("finds the closest anomaly and normal patterns", () => {
    const matcher = new ReferenceMatcher(set([
      { vector: snap, source: { recordingId: "snaps" } },
      { vector: hum, source: { recordingId: "room", label: "Normal", kind: null, group: -1 } },
    ]))
    const score = matcher.score(Float32Array.from(snap), 12)
    expect(score.anomaly).toBeCloseTo(1)
    expect(score.anomalyIndex).toBe(0)
    expect(score.normal).toBeCloseTo(0)
    expect(score.level).toBe(12)
    expect(matcher.anomalyCount).toBe(1)
    expect(matcher.normalCount).toBe(1)
  })

  it("can leave one recording out and ignores frames that do not stand out", () => {
    const matcher = new ReferenceMatcher(set([{ vector: snap, source: { recordingId: "snaps" } }]))
    expect(matcher.score(Float32Array.from(snap), 12, emptyScore(), "snaps").anomalyIndex).toBe(-1)
    expect(matcher.score(Float32Array.from(snap), 0).anomalyIndex).toBe(-1)
  })

  it("rejects an inconsistent reference set", () => {
    const broken = set([{ vector: snap, source: {} }])
    broken.norms = new Float32Array(0)
    expect(() => new ReferenceMatcher(broken)).toThrow(RangeError)
    const invalid = set([{ vector: snap, source: {} }])
    invalid.vectors[3] = Number.NaN
    expect(() => new ReferenceMatcher(invalid)).toThrow(RangeError)
  })
})

describe("decisions", () => {
  const base = { ...emptyScore(), anomaly: 0.9, anomalyIndex: 0, normal: 0.2, level: 10 }

  it("needs level, similarity and a margin over normal sounds", () => {
    expect(decide(base, DEFAULT_MATCH_SETTINGS)).toBe("anomaly")
    expect(decide({ ...base, level: 1 }, DEFAULT_MATCH_SETTINGS)).toBeNull()
    expect(decide({ ...base, anomaly: 0.7 }, DEFAULT_MATCH_SETTINGS)).toBeNull()
    expect(decide({ ...base, normal: 0.85 }, DEFAULT_MATCH_SETTINGS)).toBeNull()
  })

  it("reports unknown sounds only when enabled", () => {
    const strange = { ...base, anomaly: 0.3, normal: 0.2 }
    expect(decide(strange, DEFAULT_MATCH_SETTINGS)).toBeNull()
    expect(decide(strange, { ...DEFAULT_MATCH_SETTINGS, unknownBelow: 0.5 })).toBe("unknown")
    expect(decide({ ...strange, level: 0 }, { ...DEFAULT_MATCH_SETTINGS, unknownBelow: 0.5 })).toBeNull()
  })

  it("describes the matched pattern", () => {
    const references = set([{ vector: unit({ 1: 1 }), source: { recordingId: "snaps", segmentId: "s1", kind: "Impulsive" } }])
    expect(matchFromScore(base, "anomaly", references)).toEqual({
      recordingId: "snaps", segmentId: "s1", kind: "Impulsive", similarity: 0.9, margin: 0.9 - 0.2, referenceIndex: 0,
    })
    expect(matchFromScore(base, "unknown", references)).toMatchObject({ recordingId: "", kind: "Unknown", referenceIndex: -1 })
    expect(matchFromScore(base, null, references)).toBeNull()
  })

  it.each([
    { threshold: 0 }, { threshold: 1.1 }, { threshold: Number.NaN }, { minMargin: -0.1 },
    { minLevelDb: 61 }, { unknownBelow: 1 }, { unknownBelow: Number.NaN },
  ])("rejects invalid settings %s", (patch) => {
    expect(() => validateMatchSettings({ ...DEFAULT_MATCH_SETTINGS, ...patch })).toThrow(RangeError)
  })
})

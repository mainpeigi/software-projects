import { describe, expect, it } from "vitest"

import { BAND_COUNT } from "./acoustic-features"
import {
  BackgroundTracker,
  MIN_SPREAD_DB,
  backgroundFromFrames,
  combineBackgrounds,
  createBackground,
} from "./acoustic-background"

function frames(values: readonly number[]): Float32Array {
  const bands = new Float32Array(values.length * BAND_COUNT)
  values.forEach((value, frame) => bands.fill(value, frame * BAND_COUNT, (frame + 1) * BAND_COUNT))
  return bands
}
const all = (count: number) => Array.from({ length: count }, (_, index) => index)

describe("background model", () => {
  it("has no background without frames", () => {
    expect(backgroundFromFrames(frames([1, 2]), [])).toBeNull()
  })

  it("uses the per-band median and the typical fluctuation", () => {
    const values = all(200).map((index) => 10 + (index % 2 ? 1 : -1))
    const model = backgroundFromFrames(frames(values), all(200))!
    expect(model.level[0]).toBeCloseTo(10)
    expect(model.spread[BAND_COUNT - 1]).toBeCloseTo(1.2533, 3)
  })

  it("is not pulled up by a few loud events", () => {
    const values = all(200).map((index) => index % 20 === 0 ? 60 : 10 + (index % 2 ? 1 : -1))
    const model = backgroundFromFrames(frames(values), all(200))!
    // Ten loud frames out of 200 move the median by at most one step of the alternating signal.
    expect(model.level[0]).toBeGreaterThanOrEqual(9)
    expect(model.level[0]).toBeLessThanOrEqual(11)
    expect(model.spread[0]).toBeLessThan(3)
  })

  it("never reports a spread below the floor", () => {
    const model = backgroundFromFrames(frames(all(50).map(() => -40)), all(50))!
    expect(model.spread[0]).toBe(MIN_SPREAD_DB)
  })

  it("combines recordings by per-band median", () => {
    const combined = combineBackgrounds([createBackground(new Array(BAND_COUNT).fill(-50), new Array(BAND_COUNT).fill(1)),
      createBackground(new Array(BAND_COUNT).fill(-40), new Array(BAND_COUNT).fill(2)),
      createBackground(new Array(BAND_COUNT).fill(-10), new Array(BAND_COUNT).fill(9))])
    expect(combined.level[3]).toBe(-40)
    expect(combined.spread[3]).toBe(2)
  })
})

describe("BackgroundTracker", () => {
  const louder = new Array(BAND_COUNT).fill(10)

  it("drifts towards the room by at most its slew rate", () => {
    const tracker = new BackgroundTracker(createBackground(new Array(BAND_COUNT).fill(0), new Array(BAND_COUNT).fill(1)), { adapt: true })
    tracker.update(louder, 0.1)
    expect(tracker.model.level[0]).toBeCloseTo(0.3)
    for (let step = 0; step < 100; step += 1) tracker.update(louder, 0.1)
    expect(tracker.model.level[0]).toBeCloseTo(10)
  })

  it("ignores frames while frozen or when adaptation is off", () => {
    const initial = createBackground(new Array(BAND_COUNT).fill(0), new Array(BAND_COUNT).fill(1))
    const frozen = new BackgroundTracker(initial, { adapt: true })
    frozen.update(louder, 1, true)
    expect(frozen.model.level[0]).toBe(0)
    const fixed = new BackgroundTracker(initial, { adapt: false })
    fixed.update(louder, 1)
    expect(fixed.model.level[0]).toBe(0)
    expect(initial.level[0]).toBe(0)
  })
})

import { BAND_COUNT, FLOOR_DB } from "./acoustic-features"

/**
 * Background model: per-band level (median, dB) and spread (typical
 * fluctuation, dB). Matching compares how a sound differs from the room, so
 * both references and live audio are expressed relative to one of these.
 */
export type BackgroundModel = {
    level: Float32Array
    spread: Float32Array
}

/** Floor for the per-band fluctuation estimate. */
export const MIN_SPREAD_DB = 0.5
/** A band must rise this many spreads above its background before it counts. */
export const ALLOWANCE_SPREADS = 2
/** Mean absolute deviation of a Gaussian times this factor gives sigma. */
const MEAN_DEVIATION_TO_SIGMA = 1.2533
const MEDIAN_DEVIATION_TO_SIGMA = 1.4826
/** Deviations above this many sigmas are clipped (winsorised) when estimating spread. */
const DEVIATION_CLIP_SIGMAS = 4
const MAX_PROFILE_FRAMES = 4000

export function createBackground(level?: ArrayLike<number>, spread?: ArrayLike<number>): BackgroundModel {
    const model = {
        level: new Float32Array(BAND_COUNT).fill(FLOOR_DB),
        spread: new Float32Array(BAND_COUNT).fill(MIN_SPREAD_DB),
    }
    if (level) model.level.set(Array.from(level).slice(0, BAND_COUNT))
    if (spread) model.spread.set(Array.from(spread).slice(0, BAND_COUNT).map((value) => Math.max(MIN_SPREAD_DB, value)))
    return model
}

export function cloneBackground(model: BackgroundModel): BackgroundModel {
    return { level: Float32Array.from(model.level), spread: Float32Array.from(model.spread) }
}

function medianOfSorted(values: Float64Array): number {
    const middle = values.length >> 1
    return values.length % 2 ? values[middle] : (values[middle - 1] + values[middle]) / 2
}

/**
 * Robust background of the selected frames. `bands` is frame-major:
 * value(frame, band) = bands[frame * BAND_COUNT + band]. Returns null when no
 * frame is selected.
 */
export function backgroundFromFrames(bands: Float32Array, frames: readonly number[]): BackgroundModel | null {
    if (frames.length === 0) return null
    const stride = Math.ceil(frames.length / MAX_PROFILE_FRAMES)
    const selected = stride === 1 ? frames : frames.filter((_, index) => index % stride === 0)
    const values = new Float64Array(selected.length)
    const deviations = new Float64Array(selected.length)
    const model = createBackground()
    for (let band = 0; band < BAND_COUNT; band += 1) {
        for (let index = 0; index < selected.length; index += 1) {
            values[index] = bands[selected[index] * BAND_COUNT + band]
        }
        values.sort()
        const median = medianOfSorted(values)
        for (let index = 0; index < values.length; index += 1) deviations[index] = Math.abs(values[index] - median)
        deviations.sort()
        const clip = DEVIATION_CLIP_SIGMAS * Math.max(MIN_SPREAD_DB, MEDIAN_DEVIATION_TO_SIGMA * medianOfSorted(deviations))
        let total = 0
        for (let index = 0; index < deviations.length; index += 1) total += Math.min(clip, deviations[index])
        model.level[band] = median
        model.spread[band] = Math.max(MIN_SPREAD_DB, MEAN_DEVIATION_TO_SIGMA * (total / deviations.length))
    }
    return model
}

/** Per-band median of several backgrounds, e.g. one per recording in a dataset. */
export function combineBackgrounds(models: readonly BackgroundModel[]): BackgroundModel {
    if (models.length === 0) return createBackground()
    const model = createBackground()
    const values = new Float64Array(models.length)
    for (let band = 0; band < BAND_COUNT; band += 1) {
        models.forEach((item, index) => { values[index] = item.level[band] })
        values.sort()
        model.level[band] = medianOfSorted(values)
        models.forEach((item, index) => { values[index] = item.spread[band] })
        values.sort()
        model.spread[band] = Math.max(MIN_SPREAD_DB, medianOfSorted(values))
    }
    return model
}

export type BackgroundTrackerOptions = {
    /** When false the initial model is used unchanged. */
    adapt: boolean
    /** Maximum drift of the tracked level. */
    slewDbPerSecond?: number
    /** Time constant of the spread estimate. */
    spreadSeconds?: number
}

/**
 * Follows the live room. The level moves towards each frame by at most a
 * fixed slew, which converges on the median and ignores short sounds. The
 * detector freezes it while a known anomaly is being matched, so a sustained
 * anomaly is not learned as background.
 */
export class BackgroundTracker {
    readonly model: BackgroundModel
    private readonly deviation: Float32Array
    private readonly adapt: boolean
    private readonly slew: number
    private readonly spreadSeconds: number

    constructor(initial: BackgroundModel, { adapt, slewDbPerSecond = 3, spreadSeconds = 5 }: BackgroundTrackerOptions) {
        this.model = cloneBackground(initial)
        this.deviation = Float32Array.from(this.model.spread, (value) => value / MEAN_DEVIATION_TO_SIGMA)
        this.adapt = adapt
        this.slew = slewDbPerSecond
        this.spreadSeconds = spreadSeconds
    }

    update(bands: ArrayLike<number>, dtSeconds: number, frozen = false): void {
        if (!this.adapt || frozen || !(dtSeconds > 0)) return
        const step = this.slew * dtSeconds
        const alpha = 1 - Math.exp(-dtSeconds / this.spreadSeconds)
        const { level, spread } = this.model
        for (let band = 0; band < BAND_COUNT; band += 1) {
            const value = bands[band]
            if (!Number.isFinite(value)) continue
            const difference = value - level[band]
            level[band] += difference > 0 ? Math.min(step, difference) : Math.max(-step, difference)
            const clipped = Math.min(Math.abs(difference), DEVIATION_CLIP_SIGMAS * spread[band])
            this.deviation[band] += alpha * (clipped - this.deviation[band])
            spread[band] = Math.max(MIN_SPREAD_DB, MEAN_DEVIATION_TO_SIGMA * this.deviation[band])
        }
    }
}

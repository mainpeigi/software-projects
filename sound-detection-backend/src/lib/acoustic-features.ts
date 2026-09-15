/**
 * Frame-level audio features shared by reference extraction, the live worker,
 * offline evaluation and the recording inspector. Everything that compares
 * sounds must go through this file so both sides see identical numbers.
 */

export type AcousticFeatures = {
    bands: readonly number[]
    rmsDb: number
}

export const MATCH_SAMPLE_RATE = 48_000
export const MATCH_FRAME_SIZE = 2048
/** 10.7 ms: a 40 ms snap lands in at least three consecutive frames. */
export const MATCH_HOP_SIZE = 512
export const BAND_COUNT = 40
/** Frames stacked into one pattern: the current frame plus the two before it. */
export const CONTEXT_FRAMES = 3
export const VECTOR_LENGTH = BAND_COUNT * CONTEXT_FRAMES
/** Frames quieter than this carry no usable spectrum. */
export const SILENCE_FLOOR_DB = -65
/** Lowest value any band or level can take (digital silence). */
export const FLOOR_DB = -120

const MIN_HZ = 80
const MAX_HZ = 16_000
const MIN_POWER = 1e-12

export const FRAME_SECONDS = MATCH_FRAME_SIZE / MATCH_SAMPLE_RATE
export const HOP_SECONDS = MATCH_HOP_SIZE / MATCH_SAMPLE_RATE

const hzToMel = (hz: number) => 2595 * Math.log10(1 + hz / 700)
const melToHz = (mel: number) => 700 * (10 ** (mel / 2595) - 1)

/** BAND_COUNT + 2 triangle edges on the mel scale. */
export const bandEdgesHz: readonly number[] = Object.freeze(Array.from({ length: BAND_COUNT + 2 }, (_, index) => {
    const first = hzToMel(MIN_HZ)
    const last = hzToMel(MAX_HZ)
    return melToHz(first + ((last - first) * index) / (BAND_COUNT + 1))
}))

export const bandCentersHz: readonly number[] = Object.freeze(bandEdgesHz.slice(1, BAND_COUNT + 1))

/** Periodic Hann window, the usual choice for overlapping STFT frames. */
const hannWindow = Float64Array.from({ length: MATCH_FRAME_SIZE },
    (_, index) => 0.5 - 0.5 * Math.cos((2 * Math.PI * index) / MATCH_FRAME_SIZE))

type Filter = { first: number; weights: Float64Array; total: number }

const BIN_COUNT = MATCH_FRAME_SIZE / 2 + 1
const HZ_PER_BIN = MATCH_SAMPLE_RATE / MATCH_FRAME_SIZE

const filterbank: readonly Filter[] = Array.from({ length: BAND_COUNT }, (_, band) => {
    const left = bandEdgesHz[band]
    const center = bandEdgesHz[band + 1]
    const right = bandEdgesHz[band + 2]
    const first = Math.max(1, Math.ceil(left / HZ_PER_BIN))
    const last = Math.min(BIN_COUNT - 1, Math.floor(right / HZ_PER_BIN))
    const weights = new Float64Array(Math.max(0, last - first + 1))
    let total = 0
    for (let bin = first; bin <= last; bin += 1) {
        const hz = bin * HZ_PER_BIN
        const weight = Math.max(0, hz <= center ? (hz - left) / (center - left) : (right - hz) / (right - center))
        weights[bin - first] = weight
        total += weight
    }
    return { first, weights, total }
})

/** In-place radix-2 complex FFT with precomputed tables. */
export class Radix2Fft {
    readonly size: number
    private readonly reverse: Uint32Array
    private readonly cos: Float64Array
    private readonly sin: Float64Array

    constructor(size: number) {
        if (!Number.isInteger(size) || size < 2 || (size & (size - 1)) !== 0) {
            throw new RangeError("FFT size must be a power of two.")
        }
        this.size = size
        const bits = Math.log2(size)
        this.reverse = new Uint32Array(size)
        for (let index = 0; index < size; index += 1) {
            let reversed = 0
            for (let bit = 0; bit < bits; bit += 1) reversed = (reversed << 1) | ((index >> bit) & 1)
            this.reverse[index] = reversed
        }
        this.cos = new Float64Array(size / 2)
        this.sin = new Float64Array(size / 2)
        for (let index = 0; index < size / 2; index += 1) {
            this.cos[index] = Math.cos((2 * Math.PI * index) / size)
            this.sin[index] = -Math.sin((2 * Math.PI * index) / size)
        }
    }

    transform(real: Float64Array, imaginary: Float64Array): void {
        const size = this.size
        const reverse = this.reverse
        for (let index = 0; index < size; index += 1) {
            const target = reverse[index]
            if (index < target) {
                const re = real[index]; real[index] = real[target]; real[target] = re
                const im = imaginary[index]; imaginary[index] = imaginary[target]; imaginary[target] = im
            }
        }
        const cos = this.cos
        const sin = this.sin
        for (let length = 2; length <= size; length <<= 1) {
            const half = length >> 1
            const step = size / length
            for (let start = 0; start < size; start += length) {
                for (let offset = 0, table = 0; offset < half; offset += 1, table += step) {
                    const even = start + offset
                    const odd = even + half
                    const wr = cos[table]
                    const wi = sin[table]
                    const oddReal = real[odd] * wr - imaginary[odd] * wi
                    const oddImaginary = real[odd] * wi + imaginary[odd] * wr
                    real[odd] = real[even] - oddReal
                    imaginary[odd] = imaginary[even] - oddImaginary
                    real[even] += oddReal
                    imaginary[even] += oddImaginary
                }
            }
        }
    }
}

const sharedFft = new Radix2Fft(MATCH_FRAME_SIZE)

/**
 * Reusable per-thread analyzer. One instance per worker/loop avoids allocating
 * FFT buffers for every frame.
 */
export class FrameAnalyzer {
    private readonly real = new Float64Array(MATCH_FRAME_SIZE)
    private readonly imaginary = new Float64Array(MATCH_FRAME_SIZE)
    /** Power spectrum of the last analyzed frame, BIN_COUNT values. */
    readonly power = new Float64Array(BIN_COUNT)

    /**
     * Writes BAND_COUNT log-mel band levels (dB) into `bands` and returns the
     * frame RMS level in dB. Digital silence returns FLOOR_DB with floor bands.
     * Non-finite input returns NaN and leaves `bands` untouched.
     */
    analyze(samples: Float32Array, offset: number, bands: Float32Array | Float64Array): number {
        if (offset < 0 || offset + MATCH_FRAME_SIZE > samples.length) {
            throw new RangeError(`Matching requires exactly ${MATCH_FRAME_SIZE} samples per frame.`)
        }
        let mean = 0
        for (let index = 0; index < MATCH_FRAME_SIZE; index += 1) {
            const sample = samples[offset + index]
            if (!Number.isFinite(sample)) return Number.NaN
            mean += sample
        }
        mean /= MATCH_FRAME_SIZE

        const real = this.real
        const imaginary = this.imaginary
        let energy = 0
        for (let index = 0; index < MATCH_FRAME_SIZE; index += 1) {
            const centered = samples[offset + index] - mean
            energy += centered * centered
            real[index] = centered * hannWindow[index]
            imaginary[index] = 0
        }
        const meanPower = energy / MATCH_FRAME_SIZE
        if (meanPower <= MIN_POWER) {
            bands.fill(FLOOR_DB, 0, BAND_COUNT)
            this.power.fill(0)
            return FLOOR_DB
        }

        sharedFft.transform(real, imaginary)
        const normalization = MATCH_FRAME_SIZE * MATCH_FRAME_SIZE
        const power = this.power
        for (let index = 0; index < BIN_COUNT; index += 1) {
            power[index] = (real[index] * real[index] + imaginary[index] * imaginary[index]) / normalization
        }

        for (let band = 0; band < BAND_COUNT; band += 1) {
            const { first, weights, total } = filterbank[band]
            let weighted = 0
            for (let index = 0; index < weights.length; index += 1) weighted += power[first + index] * weights[index]
            const average = total > 0 ? weighted / total : MIN_POWER
            bands[band] = 10 * Math.log10(Math.max(MIN_POWER, average))
        }
        return 10 * Math.log10(meanPower)
    }
}

const compatibilityAnalyzer = new FrameAnalyzer()

/** One frame, validated. Returns null for silence or non-finite samples. */
export function extractAcousticFeatures(samples: Float32Array, sampleRate: number): AcousticFeatures | null {
    if (sampleRate !== MATCH_SAMPLE_RATE) {
        throw new RangeError("Matching audio must be resampled to 48000 Hz.")
    }
    if (samples.length !== MATCH_FRAME_SIZE) {
        throw new RangeError(`Matching requires exactly ${MATCH_FRAME_SIZE} samples per frame.`)
    }
    const bands = new Float64Array(BAND_COUNT)
    const rmsDb = compatibilityAnalyzer.analyze(samples, 0, bands)
    if (!Number.isFinite(rmsDb) || rmsDb <= FLOOR_DB) return null
    return { bands: Array.from(bands), rmsDb }
}

/** Number of whole frames at MATCH_HOP_SIZE in a clip of `length` samples. */
export function frameCountFor(length: number, hop = MATCH_HOP_SIZE): number {
    return length < MATCH_FRAME_SIZE ? 0 : Math.floor((length - MATCH_FRAME_SIZE) / hop) + 1
}

/** Start and end of frame `index` in milliseconds. */
export function frameSpanMs(index: number, hop = MATCH_HOP_SIZE): { startMs: number; endMs: number } {
    const startMs = (index * hop * 1000) / MATCH_SAMPLE_RATE
    return { startMs, endMs: startMs + FRAME_SECONDS * 1000 }
}

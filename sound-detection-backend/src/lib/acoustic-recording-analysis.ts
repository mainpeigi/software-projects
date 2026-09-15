import {
    BAND_COUNT,
    FRAME_SECONDS,
    FrameAnalyzer,
    HOP_SECONDS,
    MATCH_FRAME_SIZE,
    MATCH_HOP_SIZE,
    MATCH_SAMPLE_RATE,
    frameCountFor,
} from "./acoustic-features"
import { ALLOWANCE_SPREADS, backgroundFromFrames, createBackground, type BackgroundModel } from "./acoustic-background"
import { frameExcess, type MatchKind } from "./acoustic-matching"

/**
 * Whole-recording analysis at the live hop. The inspector draws it and the
 * reference extractor reads the same frames, so what the operator sees is
 * exactly what the detector learns from.
 */

export type SegmentKind = "Auto" | "Impulsive" | "Sustained"
export type TimeRange = { startMs: number; endMs: number }
export type AnnotatedRange = TimeRange & { kind: SegmentKind }

export const HOP_MS = HOP_SECONDS * 1000
export const FRAME_MS = FRAME_SECONDS * 1000
/** Rows of the display spectrogram, log-spaced between the display limits. */
export const DISPLAY_ROWS = 128
export const DISPLAY_MIN_HZ = 40
export const DISPLAY_MAX_HZ = 20_000
export const DISPLAY_FLOOR_DB = -110
export const DISPLAY_CEILING_DB = -10
/** Samples per waveform envelope block (5.3 ms). */
export const ENVELOPE_BLOCK = 256
/** Segments shorter than this are treated as impulsive when their kind is Auto. */
export const IMPULSIVE_MAX_MS = 150
/** Automatic onsets: frames rising this far above the recording background. */
export const ONSET_LEVEL_DB = 6
/** Background frames keep this distance from any segment, including stacked context. */
const SEGMENT_GUARD_MS = 20
const YIELD_FRAMES = 256

export type RecordingAnalysis = {
    durationMs: number
    frameCount: number
    /** frameCount x BAND_COUNT log-mel levels (dB), frame-major. */
    bands: Float32Array
    /** Frame RMS level (dB). */
    rmsDb: Float32Array
    /** frameCount x DISPLAY_ROWS, 0..255 between DISPLAY_FLOOR_DB and DISPLAY_CEILING_DB. */
    spectrogram: Uint8Array
    /** Interleaved [min, max] per ENVELOPE_BLOCK samples. */
    envelope: Float32Array
}

const BIN_COUNT = MATCH_FRAME_SIZE / 2 + 1
const HZ_PER_BIN = MATCH_SAMPLE_RATE / MATCH_FRAME_SIZE

export const displayRowEdgesHz: readonly number[] = Object.freeze(Array.from({ length: DISPLAY_ROWS + 1 },
    (_, index) => DISPLAY_MIN_HZ * (DISPLAY_MAX_HZ / DISPLAY_MIN_HZ) ** (index / DISPLAY_ROWS)))

const displayRows = Array.from({ length: DISPLAY_ROWS }, (_, row) => {
    const low = Math.min(BIN_COUNT - 1, Math.max(1, Math.floor(displayRowEdgesHz[row] / HZ_PER_BIN)))
    const high = Math.min(BIN_COUNT - 1, Math.max(low, Math.ceil(displayRowEdgesHz[row + 1] / HZ_PER_BIN) - 1))
    return [low, high] as const
})

function envelopeOf(samples: Float32Array): Float32Array {
    const blocks = Math.ceil(samples.length / ENVELOPE_BLOCK)
    const envelope = new Float32Array(blocks * 2)
    for (let block = 0; block < blocks; block += 1) {
        let low = Infinity
        let high = -Infinity
        const end = Math.min(samples.length, (block + 1) * ENVELOPE_BLOCK)
        for (let index = block * ENVELOPE_BLOCK; index < end; index += 1) {
            const value = samples[index]
            if (value < low) low = value
            if (value > high) high = value
        }
        envelope[block * 2] = Number.isFinite(low) ? low : 0
        envelope[block * 2 + 1] = Number.isFinite(high) ? high : 0
    }
    return envelope
}

class RecordingAnalyzer {
    readonly result: RecordingAnalysis
    private readonly analyzer = new FrameAnalyzer()
    private readonly samples: Float32Array
    private readonly display: boolean
    private next = 0

    constructor(samples: Float32Array, display: boolean) {
        this.samples = samples
        this.display = display
        const frameCount = frameCountFor(samples.length)
        this.result = {
            durationMs: (samples.length * 1000) / MATCH_SAMPLE_RATE,
            frameCount,
            bands: new Float32Array(frameCount * BAND_COUNT),
            rmsDb: new Float32Array(frameCount),
            spectrogram: new Uint8Array(display ? frameCount * DISPLAY_ROWS : 0),
            envelope: display ? envelopeOf(samples) : new Float32Array(0),
        }
    }

    get done() { return this.next >= this.result.frameCount }
    get completed() { return this.next }

    step(maximum: number): void {
        const { bands, rmsDb, spectrogram, frameCount } = this.result
        const power = this.analyzer.power
        const scale = 255 / (DISPLAY_CEILING_DB - DISPLAY_FLOOR_DB)
        const end = Math.min(frameCount, this.next + maximum)
        for (let frame = this.next; frame < end; frame += 1) {
            const level = this.analyzer.analyze(this.samples, frame * MATCH_HOP_SIZE, bands.subarray(frame * BAND_COUNT, (frame + 1) * BAND_COUNT))
            if (!Number.isFinite(level)) throw new Error("The recording contains invalid samples.")
            rmsDb[frame] = level
            if (!this.display) continue
            const base = frame * DISPLAY_ROWS
            for (let row = 0; row < DISPLAY_ROWS; row += 1) {
                const [low, high] = displayRows[row]
                let loudest = 0
                for (let bin = low; bin <= high; bin += 1) if (power[bin] > loudest) loudest = power[bin]
                const db = loudest > 0 ? 10 * Math.log10(loudest) : DISPLAY_FLOOR_DB
                spectrogram[base + row] = Math.max(0, Math.min(255, Math.round((db - DISPLAY_FLOOR_DB) * scale)))
            }
        }
        this.next = end
    }
}

/** `display: false` skips the spectrogram and waveform (reference extraction needs neither). */
export function analyzeRecordingSync(samples: Float32Array, { display = true }: { display?: boolean } = {}): RecordingAnalysis {
    const analyzer = new RecordingAnalyzer(samples, display)
    analyzer.step(Number.MAX_SAFE_INTEGER)
    return analyzer.result
}

/** Same as analyzeRecordingSync, yielding to the event loop so the page stays responsive. */
export async function analyzeRecording(samples: Float32Array, { signal, onProgress, display = true }: {
    signal?: AbortSignal
    onProgress?: (completed: number, total: number) => void
    display?: boolean
} = {}): Promise<RecordingAnalysis> {
    signal?.throwIfAborted()
    const analyzer = new RecordingAnalyzer(samples, display)
    while (!analyzer.done) {
        analyzer.step(YIELD_FRAMES)
        onProgress?.(analyzer.completed, analyzer.result.frameCount)
        await new Promise<void>((resolve) => setTimeout(resolve, 0))
        signal?.throwIfAborted()
    }
    return analyzer.result
}

export const frameStartMs = (index: number) => index * HOP_MS
export const frameCenterMs = (index: number) => index * HOP_MS + FRAME_MS / 2

/** Frame whose centre is closest to `timeMs`. */
export function frameIndexAt(analysis: RecordingAnalysis, timeMs: number): number {
    if (analysis.frameCount === 0) return -1
    return Math.max(0, Math.min(analysis.frameCount - 1, Math.round((timeMs - FRAME_MS / 2) / HOP_MS)))
}

export function frameBands(analysis: RecordingAnalysis, index: number): Float32Array {
    return analysis.bands.subarray(index * BAND_COUNT, (index + 1) * BAND_COUNT)
}

/**
 * Frames that belong to a range: they overlap it by at least half of the
 * shorter of the range and the frame, so a 40 ms snap still yields frames.
 */
export function segmentFrames(analysis: RecordingAnalysis, range: TimeRange): number[] {
    const length = range.endMs - range.startMs
    if (!(length > 0) || analysis.frameCount === 0) return []
    const needed = 0.5 * Math.min(length, FRAME_MS) - 1e-6
    const first = Math.max(0, Math.floor((range.startMs - FRAME_MS) / HOP_MS))
    const last = Math.min(analysis.frameCount - 1, Math.ceil(range.endMs / HOP_MS))
    const frames: number[] = []
    for (let frame = first; frame <= last; frame += 1) {
        const start = frameStartMs(frame)
        const overlap = Math.min(start + FRAME_MS, range.endMs) - Math.max(start, range.startMs)
        if (overlap >= needed) frames.push(frame)
    }
    return frames
}

/** Frames whose whole stacked context stays clear of every range. */
export function backgroundFrames(analysis: RecordingAnalysis, ranges: readonly TimeRange[]): number[] {
    const clear = new Uint8Array(analysis.frameCount).fill(1)
    for (const range of ranges) {
        const guardStart = range.startMs - SEGMENT_GUARD_MS
        const guardEnd = range.endMs + SEGMENT_GUARD_MS
        const first = Math.max(0, Math.floor((guardStart - FRAME_MS) / HOP_MS))
        const last = Math.min(analysis.frameCount - 1, Math.ceil(guardEnd / HOP_MS) + 2)
        for (let frame = first; frame <= last; frame += 1) {
            const contextStart = frameStartMs(frame - 2)
            const contextEnd = frameStartMs(frame) + FRAME_MS
            if (contextStart < guardEnd && contextEnd > guardStart) clear[frame] = 0
        }
    }
    const frames: number[] = []
    clear.forEach((value, frame) => { if (value) frames.push(frame) })
    return frames
}

/** Background of a recording, learned from everything outside the ranges. */
export function recordingBackground(analysis: RecordingAnalysis, ranges: readonly TimeRange[]): BackgroundModel {
    let frames = backgroundFrames(analysis, ranges)
    if (frames.length < 8) {
        const inside = new Set(ranges.flatMap((range) => segmentFrames(analysis, range)))
        frames = Array.from({ length: analysis.frameCount }, (_, frame) => frame).filter((frame) => !inside.has(frame))
    }
    if (frames.length < 8) {
        const quietest = Array.from({ length: analysis.frameCount }, (_, frame) => frame)
            .sort((left, right) => analysis.rmsDb[left] - analysis.rmsDb[right])
        frames = quietest.slice(0, Math.max(1, Math.ceil(quietest.length / 4)))
    }
    return backgroundFromFrames(analysis.bands, frames) ?? createBackground()
}

/**
 * Level above background (dB) of every frame on its own, without context. The
 * background comes from the same recording, so no gain offset is discounted
 * and a broadband burst shaped like the room still stands out.
 */
export function frameLevels(analysis: RecordingAnalysis, background: BackgroundModel): Float32Array {
    const scratch = new Float32Array(BAND_COUNT)
    const levels = new Float32Array(analysis.frameCount)
    for (let frame = 0; frame < analysis.frameCount; frame += 1) {
        levels[frame] = frameExcess(frameBands(analysis, frame), background, scratch)
    }
    return levels
}

export type OnsetRegion = TimeRange & { peakMs: number; peakLevelDb: number }

/** Regions where the recording stands out from its own background. */
export function detectOnsets(analysis: RecordingAnalysis, background: BackgroundModel = recordingBackground(analysis, []), {
    levelDb = ONSET_LEVEL_DB, mergeGapMs = 50, maxRegions = 200,
}: { levelDb?: number; mergeGapMs?: number; maxRegions?: number } = {}): OnsetRegion[] {
    const levels = frameLevels(analysis, background)
    const spans: { first: number; last: number }[] = []
    for (let frame = 0; frame < levels.length; frame += 1) {
        if (levels[frame] < levelDb) continue
        const previous = spans.at(-1)
        if (previous && (frame - previous.last) * HOP_MS <= mergeGapMs) previous.last = frame
        else spans.push({ first: frame, last: frame })
    }
    let regions = spans.map(({ first, last }) => {
        let peak = first
        for (let frame = first; frame <= last; frame += 1) if (levels[frame] > levels[peak]) peak = frame
        let startMs = Math.max(0, frameCenterMs(first) - HOP_MS / 2 - 5)
        let endMs = Math.min(analysis.durationMs, frameCenterMs(last) + HOP_MS / 2 + 5)
        if (endMs - startMs < 20) {
            const center = (startMs + endMs) / 2
            startMs = Math.max(0, center - 10)
            endMs = Math.min(analysis.durationMs, startMs + 20)
        }
        return { startMs, endMs, peakMs: frameCenterMs(peak), peakLevelDb: levels[peak] }
    })
    if (regions.length > maxRegions) {
        regions = [...regions].sort((left, right) => right.peakLevelDb - left.peakLevelDb).slice(0, maxRegions)
            .sort((left, right) => left.startMs - right.startMs)
    }
    return regions
}

/** Onset region nearest to `timeMs`, within `maxDistanceMs`. */
export function nearestOnset(onsets: readonly OnsetRegion[], timeMs: number, maxDistanceMs = 500): OnsetRegion | null {
    let best: OnsetRegion | null = null
    let bestDistance = Infinity
    for (const onset of onsets) {
        const distance = timeMs < onset.startMs ? onset.startMs - timeMs : timeMs > onset.endMs ? timeMs - onset.endMs : 0
        if (distance < bestDistance) { best = onset; bestDistance = distance }
    }
    return bestDistance <= maxDistanceMs ? best : null
}

export function resolveKind(range: AnnotatedRange): MatchKind {
    if (range.kind !== "Auto") return range.kind
    return range.endMs - range.startMs < IMPULSIVE_MAX_MS ? "Impulsive" : "Sustained"
}

export type RangeProfile = {
    frames: number
    /** Power-mean level per band over the range (dB). */
    meanBands: Float32Array
    /** Loudest level per band over the range (dB). */
    peakBands: Float32Array
    /** meanBands minus background level (dB, signed). */
    difference: Float32Array
    /** How far a band may fluctuate before it counts (dB). */
    allowance: Float32Array
    peakLevelDb: number
    meanLevelDb: number
}

/** Spectrum and level statistics of a range, relative to a background. */
export function rangeProfile(analysis: RecordingAnalysis, range: TimeRange, background: BackgroundModel): RangeProfile | null {
    let frames = segmentFrames(analysis, range)
    if (frames.length === 0) {
        const nearest = frameIndexAt(analysis, (range.startMs + range.endMs) / 2)
        if (nearest < 0) return null
        frames = [nearest]
    }
    const power = new Float64Array(BAND_COUNT)
    const peakBands = new Float32Array(BAND_COUNT).fill(-Infinity)
    const scratch = new Float32Array(BAND_COUNT)
    let peakLevelDb = 0
    let levelTotal = 0
    for (const frame of frames) {
        const bands = frameBands(analysis, frame)
        for (let band = 0; band < BAND_COUNT; band += 1) {
            power[band] += 10 ** (bands[band] / 10)
            if (bands[band] > peakBands[band]) peakBands[band] = bands[band]
        }
        const level = frameExcess(bands, background, scratch)
        peakLevelDb = Math.max(peakLevelDb, level)
        levelTotal += level
    }
    const meanBands = Float32Array.from(power, (value) => 10 * Math.log10(value / frames.length))
    return {
        frames: frames.length,
        meanBands,
        peakBands,
        difference: Float32Array.from(meanBands, (value, band) => value - background.level[band]),
        allowance: Float32Array.from(background.spread, (value) => ALLOWANCE_SPREADS * value),
        peakLevelDb,
        meanLevelDb: levelTotal / frames.length,
    }
}

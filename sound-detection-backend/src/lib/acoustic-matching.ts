import { BAND_COUNT, CONTEXT_FRAMES, HOP_SECONDS, VECTOR_LENGTH } from "./acoustic-features"
import { ALLOWANCE_SPREADS, type BackgroundModel } from "./acoustic-background"

export type ReferenceLabel = "Normal" | "Anomaly"
export type MatchKind = "Impulsive" | "Sustained"

/** Where one reference pattern came from. */
export type ReferenceSource = {
    recordingId: string
    /** Annotated segment, or null for implicit background and unannotated audio. */
    segmentId: string | null
    label: ReferenceLabel
    /** Gate mode for anomaly references; null for normal references. */
    kind: MatchKind | null
    /** Normal pattern taken from outside the segments of an Anomaly recording. */
    implicit: boolean
    /** Time of the pattern's newest frame inside its recording. */
    timeMs: number
    /** Dataset-wide index of the occurrence (segment) it belongs to; -1 for normal patterns. */
    group: number
}

/**
 * Compiled reference patterns. Row r of `vectors` (VECTOR_LENGTH values) is a
 * unit-length stack of CONTEXT_FRAMES excess spectra; `levels[r]` is its level
 * above background in dB.
 */
export type ReferenceSet = {
    vectors: Float32Array
    levels: Float32Array
    /** Length of each pattern before normalisation, to show it in dB again. */
    norms: Float32Array
    sources: ReferenceSource[]
    /** Median of the recordings' backgrounds; the live detector starts from it. */
    background: BackgroundModel
    /** Per-recording backgrounds, used to leave one recording out when evaluating. */
    recordingBackgrounds: Record<string, BackgroundModel>
}

export const EXCESS_CAP_DB = 60
/** The level of a frame is the mean excess of its most prominent bands. */
export const LEVEL_TOP_BANDS = 4

const ordered = new Float32Array(BAND_COUNT)

/**
 * Level change shared by at least three quarters of the valid bands in one
 * frame: what a gain change looks like. Zero when most bands did not move
 * together.
 */
export function commonShift(bands: ArrayLike<number>, background: BackgroundModel): number {
    let valid = 0
    for (let band = 0; band < BAND_COUNT; band += 1) {
        const difference = bands[band] - background.level[band]
        if (Number.isFinite(difference)) ordered[valid++] = difference
    }
    if (valid === 0) return 0
    const values = ordered.subarray(0, valid)
    values.sort()
    const quarter = Math.floor(valid / 4)
    const low = values[quarter]
    const high = values[valid - 1 - quarter]
    return low > 0 ? low : high < 0 ? high : 0
}

/**
 * Writes how far each band rises above background, beyond its normal
 * fluctuation and beyond a gain offset `shift` (dB), into
 * `out[offset..offset+BAND_COUNT)`. Invalid bands count as zero. Returns the
 * frame level (mean of the LEVEL_TOP_BANDS largest excesses, dB).
 */
export function frameExcess(bands: ArrayLike<number>, background: BackgroundModel, out: Float32Array, offset = 0, shift = 0): number {
    let first = 0
    let second = 0
    let third = 0
    let fourth = 0
    for (let band = 0; band < BAND_COUNT; band += 1) {
        const raw = bands[band] - background.level[band] - shift - ALLOWANCE_SPREADS * background.spread[band]
        const excess = Number.isFinite(raw) ? Math.min(EXCESS_CAP_DB, Math.max(0, raw)) : 0
        out[offset + band] = excess
        if (excess > fourth) {
            if (excess > first) { fourth = third; third = second; second = first; first = excess }
            else if (excess > second) { fourth = third; third = second; second = excess }
            else if (excess > third) { fourth = third; third = excess }
            else fourth = excess
        }
    }
    return (first + second + third + fourth) / LEVEL_TOP_BANDS
}

/** Normalises in place and returns the original length; zero vectors stay zero. */
export function normalizeInPlace(vector: Float32Array, offset = 0, length = VECTOR_LENGTH): number {
    let energy = 0
    for (let index = 0; index < length; index += 1) energy += vector[offset + index] * vector[offset + index]
    const norm = Math.sqrt(energy)
    if (!(norm > 1e-6)) {
        vector.fill(0, offset, offset + length)
        return 0
    }
    for (let index = 0; index < length; index += 1) vector[offset + index] /= norm
    return norm
}

/** How fast the tracked gain offset may follow a change shared by most bands. */
export const GAIN_SLEW_DB_PER_SECOND = 10

/**
 * Keeps the last CONTEXT_FRAMES excess spectra and produces the stacked
 * pattern (oldest first). Missing history counts as "nothing above background".
 *
 * It also tracks a gain offset: a level change shared by most bands. The
 * offset locks on at the first frame (a different microphone gain than the
 * recording) and then follows at GAIN_SLEW_DB_PER_SECOND, so a steady gain
 * change is discounted while a short broadband burst still stands out.
 */
export class ExcessHistory {
    private readonly frames = new Float32Array(VECTOR_LENGTH)
    private readonly levels = new Float32Array(CONTEXT_FRAMES)
    private gain: number | null = null

    /** Forgets the stacked context; the gain offset belongs to the microphone and is kept. */
    clear(): void {
        this.frames.fill(0)
        this.levels.fill(0)
    }

    get gainDb(): number { return this.gain ?? 0 }

    /**
     * Adds a frame; returns the stack level (loudest frame in the stack).
     * `frozen` stops the gain offset from learning, e.g. while an anomaly is matched.
     */
    push(bands: ArrayLike<number>, background: BackgroundModel, { dtSeconds = HOP_SECONDS, frozen = false }: { dtSeconds?: number; frozen?: boolean } = {}): number {
        const instant = commonShift(bands, background)
        if (this.gain === null) this.gain = instant
        else if (!frozen && dtSeconds > 0) {
            const step = GAIN_SLEW_DB_PER_SECOND * dtSeconds
            this.gain += Math.max(-step, Math.min(step, instant - this.gain))
        }
        this.frames.copyWithin(0, BAND_COUNT)
        this.levels.copyWithin(0, 1)
        this.levels[CONTEXT_FRAMES - 1] = frameExcess(bands, background, this.frames, (CONTEXT_FRAMES - 1) * BAND_COUNT, this.gain)
        return Math.max(...this.levels)
    }

    /** Excess spectrum of the newest frame. */
    current(): Float32Array {
        return this.frames.subarray((CONTEXT_FRAMES - 1) * BAND_COUNT)
    }

    /** Copies the unit-length stacked pattern into `out` and returns its original norm. */
    vector(out: Float32Array, offset = 0): number {
        out.set(this.frames, offset)
        return normalizeInPlace(out, offset)
    }
}

export type FrameScore = {
    /** Best similarity to an Anomaly pattern (cosine, 0..1). */
    anomaly: number
    anomalyIndex: number
    /** Best similarity to a Normal pattern; 0 when the set has none. */
    normal: number
    normalIndex: number
    /** Stack level above background, dB. */
    level: number
}

export const emptyScore = (): FrameScore => ({ anomaly: 0, anomalyIndex: -1, normal: 0, normalIndex: -1, level: 0 })

export function validateReferenceSet(set: ReferenceSet): void {
    const count = set.sources.length
    if (set.vectors.length !== count * VECTOR_LENGTH || set.levels.length !== count || set.norms.length !== count) {
        throw new RangeError("Invalid acoustic reference set.")
    }
    for (let index = 0; index < set.vectors.length; index += 1) {
        if (!Number.isFinite(set.vectors[index])) throw new RangeError("Acoustic references contain invalid values.")
    }
    if (set.background.level.length !== BAND_COUNT || set.background.spread.length !== BAND_COUNT) {
        throw new RangeError("Invalid acoustic background.")
    }
}

/** Nearest-pattern scoring against a compiled reference set. */
export class ReferenceMatcher {
    readonly set: ReferenceSet
    private readonly anomalyRows: Int32Array
    private readonly normalRows: Int32Array
    private readonly rowRecording: Int32Array
    private readonly recordingIndex = new Map<string, number>()

    constructor(set: ReferenceSet) {
        validateReferenceSet(set)
        this.set = set
        const anomaly: number[] = []
        const normal: number[] = []
        this.rowRecording = new Int32Array(set.sources.length)
        set.sources.forEach((source, row) => {
            let index = this.recordingIndex.get(source.recordingId)
            if (index === undefined) {
                index = this.recordingIndex.size
                this.recordingIndex.set(source.recordingId, index)
            }
            this.rowRecording[row] = index
            if (!(set.levels[row] > 0)) return
            if (source.label === "Anomaly") anomaly.push(row)
            else normal.push(row)
        })
        this.anomalyRows = Int32Array.from(anomaly)
        this.normalRows = Int32Array.from(normal)
    }

    get anomalyCount(): number { return this.anomalyRows.length }
    get normalCount(): number { return this.normalRows.length }

    /** Similarity of a unit `vector` to one reference row. */
    similarity(vector: Float32Array, row: number, offset = 0): number {
        const vectors = this.set.vectors
        const base = row * VECTOR_LENGTH
        let dot = 0
        for (let index = 0; index < VECTOR_LENGTH; index += 1) dot += vector[offset + index] * vectors[base + index]
        return Math.max(0, Math.min(1, dot))
    }

    /**
     * Scores a unit-length live pattern. `excludeRecordingId` leaves one
     * recording's patterns out, for offline evaluation.
     */
    score(vector: Float32Array, level: number, out: FrameScore = emptyScore(), excludeRecordingId: string | null = null): FrameScore {
        out.level = level
        out.anomaly = 0
        out.anomalyIndex = -1
        out.normal = 0
        out.normalIndex = -1
        if (!(level > 0)) return out
        const excluded = excludeRecordingId === null ? -1 : this.recordingIndex.get(excludeRecordingId) ?? -1
        const vectors = this.set.vectors
        const scan = (rows: Int32Array, anomaly: boolean) => {
            for (let item = 0; item < rows.length; item += 1) {
                const row = rows[item]
                if (this.rowRecording[row] === excluded) continue
                const base = row * VECTOR_LENGTH
                let dot = 0
                for (let index = 0; index < VECTOR_LENGTH; index += 1) dot += vector[index] * vectors[base + index]
                if (anomaly) {
                    if (dot > out.anomaly) { out.anomaly = dot; out.anomalyIndex = row }
                } else if (dot > out.normal) { out.normal = dot; out.normalIndex = row }
            }
        }
        scan(this.anomalyRows, true)
        scan(this.normalRows, false)
        out.anomaly = Math.min(1, out.anomaly)
        out.normal = Math.min(1, out.normal)
        return out
    }
}

export type MatchSettings = {
    /** Minimum similarity to an Anomaly pattern. */
    threshold: number
    /** The Anomaly pattern must beat the best Normal pattern by this much. */
    minMargin: number
    /** The sound must rise this far above the background (dB). */
    minLevelDb: number
    /** Report sounds that resemble no pattern at all below this similarity; null disables it. */
    unknownBelow: number | null
}

export const DEFAULT_MATCH_SETTINGS: Readonly<MatchSettings> = Object.freeze({
    threshold: 0.8, minMargin: 0.1, minLevelDb: 3, unknownBelow: null,
})

export function validateMatchSettings(settings: MatchSettings): MatchSettings {
    const { threshold, minMargin, minLevelDb, unknownBelow } = settings
    if (!Number.isFinite(threshold) || threshold <= 0 || threshold > 1 ||
        !Number.isFinite(minMargin) || minMargin < 0 || minMargin > 1 ||
        !Number.isFinite(minLevelDb) || minLevelDb < 0 || minLevelDb > EXCESS_CAP_DB ||
        (unknownBelow !== null && (!Number.isFinite(unknownBelow) || unknownBelow <= 0 || unknownBelow >= 1))) {
        throw new RangeError("Invalid acoustic matching settings.")
    }
    return { threshold, minMargin, minLevelDb, unknownBelow }
}

export type Decision = "anomaly" | "unknown" | null

export function decide(score: FrameScore, settings: MatchSettings): Decision {
    if (!(score.level >= settings.minLevelDb) || score.level <= 0) return null
    if (score.anomalyIndex >= 0 && score.anomaly >= settings.threshold &&
        score.anomaly - score.normal >= settings.minMargin) return "anomaly"
    if (settings.unknownBelow !== null && Math.max(score.anomaly, score.normal) < settings.unknownBelow) return "unknown"
    return null
}

export type AcousticMatch = {
    /** Matched recording; empty for an unknown sound. */
    recordingId: string
    segmentId: string | null
    kind: MatchKind | "Unknown"
    similarity: number
    /** Anomaly similarity minus best Normal similarity. */
    margin: number
    referenceIndex: number
}

export function matchFromScore(score: FrameScore, decision: Decision, set: ReferenceSet): AcousticMatch | null {
    if (decision === "anomaly") {
        const source = set.sources[score.anomalyIndex]
        return {
            recordingId: source.recordingId, segmentId: source.segmentId, kind: source.kind ?? "Sustained",
            similarity: score.anomaly, margin: score.anomaly - score.normal, referenceIndex: score.anomalyIndex,
        }
    }
    if (decision === "unknown") {
        return {
            recordingId: "", segmentId: null, kind: "Unknown",
            similarity: Math.max(score.anomaly, score.normal), margin: 0, referenceIndex: -1,
        }
    }
    return null
}

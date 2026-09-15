import { BAND_COUNT, VECTOR_LENGTH } from "./acoustic-features"
import { combineBackgrounds, type BackgroundModel } from "./acoustic-background"
import { ExcessHistory, type ReferenceLabel, type ReferenceSet, type ReferenceSource } from "./acoustic-matching"
import {
    backgroundFrames,
    detectOnsets,
    frameBands,
    frameCenterMs,
    recordingBackground,
    resolveKind,
    segmentFrames,
    type AnnotatedRange,
    type RecordingAnalysis,
} from "./acoustic-recording-analysis"

/** Anomaly frames must rise at least this far above background to become a pattern. */
export const ANOMALY_REFERENCE_MIN_LEVEL_DB = 1
/** Normal frames below this level are plain background and need no pattern. */
export const NORMAL_REFERENCE_MIN_LEVEL_DB = 1.5
export const MAX_ANOMALY_REFERENCES_PER_RECORDING = 3000
export const MAX_NORMAL_REFERENCES_PER_RECORDING = 1500
/** Bounds live CPU: every live frame is compared with every pattern. */
export const MAX_REFERENCES = 12_000

export type ExtractionSegment = AnnotatedRange & { id: string | null }

export type RecordingReferences = {
    recordingId: string
    label: ReferenceLabel
    background: BackgroundModel
    vectors: Float32Array
    levels: Float32Array
    norms: Float32Array
    /** `group` is local to the recording until buildReferenceSet renumbers it. */
    sources: ReferenceSource[]
    /** Anomaly occurrences used: annotated segments, or automatic onsets. */
    segments: ExtractionSegment[]
    /** True when an Anomaly recording had no annotations and onsets were used. */
    automatic: boolean
    /** Occurrences that did not stand out from the background at all. */
    silentSegments: number
}

class PatternCollector {
    private readonly vectors: number[][] = []
    private readonly levels: number[] = []
    private readonly norms: number[] = []
    private readonly sources: ReferenceSource[] = []

    get count() { return this.sources.length }

    push(vector: Float32Array, level: number, norm: number, source: ReferenceSource) {
        this.vectors.push(Array.from(vector))
        this.levels.push(level)
        this.norms.push(norm)
        this.sources.push(source)
    }

    /** Evenly spaced subset of at most `maximum` patterns, in time order. */
    take(maximum: number) {
        const count = this.count
        const indexes = count <= maximum
            ? Array.from({ length: count }, (_, index) => index)
            : Array.from({ length: maximum }, (_, index) => Math.floor((index * count) / maximum))
        const vectors = new Float32Array(indexes.length * VECTOR_LENGTH)
        indexes.forEach((source, row) => vectors.set(this.vectors[source], row * VECTOR_LENGTH))
        return {
            vectors,
            levels: Float32Array.from(indexes, (index) => this.levels[index]),
            norms: Float32Array.from(indexes, (index) => this.norms[index]),
            sources: indexes.map((index) => this.sources[index]),
        }
    }
}

/**
 * Turns one analysed recording into reference patterns. Anomaly recordings
 * contribute Anomaly patterns from their segments and implicit Normal patterns
 * from everything clearly outside them. Normal recordings contribute Normal
 * patterns for every sound that stands out from their own background.
 */
export function extractRecordingReferences(
    analysis: RecordingAnalysis,
    recording: { id: string; label: ReferenceLabel },
    annotated: readonly ExtractionSegment[],
): RecordingReferences {
    const anomalyRecording = recording.label === "Anomaly"
    const automatic = anomalyRecording && annotated.length === 0
    const segments: ExtractionSegment[] = !anomalyRecording ? []
        : automatic ? detectOnsets(analysis).map(({ startMs, endMs }) => ({ id: null, startMs, endMs, kind: "Auto" as const }))
        : [...annotated].sort((left, right) => left.startMs - right.startMs)
    const background = recordingBackground(analysis, segments)

    const frameSegment = new Int32Array(analysis.frameCount).fill(-1)
    segments.forEach((segment, index) => {
        for (const frame of segmentFrames(analysis, segment)) if (frameSegment[frame] < 0) frameSegment[frame] = index
    })
    const clear = new Uint8Array(analysis.frameCount)
    for (const frame of backgroundFrames(analysis, segments)) clear[frame] = 1

    const history = new ExcessHistory()
    const vector = new Float32Array(VECTOR_LENGTH)
    const anomalies = new PatternCollector()
    const normals = new PatternCollector()
    const covered = new Uint8Array(segments.length)
    for (let frame = 0; frame < analysis.frameCount; frame += 1) {
        const segmentIndex = frameSegment[frame]
        const level = history.push(frameBands(analysis, frame), background, { frozen: segmentIndex >= 0 })
        if (segmentIndex >= 0) {
            if (level < ANOMALY_REFERENCE_MIN_LEVEL_DB) continue
            const norm = history.vector(vector)
            if (norm === 0) continue
            const segment = segments[segmentIndex]
            anomalies.push(vector, level, norm, {
                recordingId: recording.id, segmentId: segment.id, label: "Anomaly", kind: resolveKind(segment),
                implicit: false, timeMs: frameCenterMs(frame), group: segmentIndex,
            })
            covered[segmentIndex] = 1
        } else if ((!anomalyRecording || clear[frame]) && level >= NORMAL_REFERENCE_MIN_LEVEL_DB) {
            const norm = history.vector(vector)
            if (norm === 0) continue
            normals.push(vector, level, norm, {
                recordingId: recording.id, segmentId: null, label: "Normal", kind: null,
                implicit: anomalyRecording, timeMs: frameCenterMs(frame), group: -1,
            })
        }
    }

    const anomaly = anomalies.take(MAX_ANOMALY_REFERENCES_PER_RECORDING)
    const normal = normals.take(MAX_NORMAL_REFERENCES_PER_RECORDING)
    const vectors = new Float32Array(anomaly.vectors.length + normal.vectors.length)
    vectors.set(anomaly.vectors)
    vectors.set(normal.vectors, anomaly.vectors.length)
    return {
        recordingId: recording.id,
        label: recording.label,
        background,
        vectors,
        levels: Float32Array.of(...anomaly.levels, ...normal.levels),
        norms: Float32Array.of(...anomaly.norms, ...normal.norms),
        sources: [...anomaly.sources, ...normal.sources],
        segments,
        automatic,
        silentSegments: covered.reduce((total, value) => total + (value ? 0 : 1), segments.length - covered.length),
    }
}

/**
 * Concatenates recordings into one set. When the set is too large for live
 * matching, Normal patterns are thinned evenly first; Anomaly patterns are
 * never dropped silently.
 */
export function buildReferenceSet(parts: readonly RecordingReferences[], maximum = MAX_REFERENCES): ReferenceSet {
    const anomalyCount = parts.reduce((total, part) => total + part.sources.filter((source) => source.label === "Anomaly").length, 0)
    const normalCount = parts.reduce((total, part) => total + part.sources.length, 0) - anomalyCount
    if (anomalyCount > maximum) {
        throw new Error("The anomaly segments contain too many sound patterns for live detection. Use shorter segments or fewer recordings.")
    }
    const normalBudget = Math.min(normalCount, maximum - anomalyCount)
    const keepNormal = (index: number, total: number) => normalBudget >= normalCount ||
        Math.floor(((index + 1) * normalBudget) / total) > Math.floor((index * normalBudget) / total)

    const rows: { part: RecordingReferences; row: number; group: number }[] = []
    let groupOffset = 0
    let normalIndex = 0
    for (const part of parts) {
        part.sources.forEach((source, row) => {
            if (source.label === "Normal" && !keepNormal(normalIndex++, normalCount)) return
            rows.push({ part, row, group: source.group >= 0 ? groupOffset + source.group : -1 })
        })
        groupOffset += part.segments.length
    }

    const vectors = new Float32Array(rows.length * VECTOR_LENGTH)
    const levels = new Float32Array(rows.length)
    const norms = new Float32Array(rows.length)
    const sources: ReferenceSource[] = []
    rows.forEach(({ part, row, group }, index) => {
        vectors.set(part.vectors.subarray(row * VECTOR_LENGTH, (row + 1) * VECTOR_LENGTH), index * VECTOR_LENGTH)
        levels[index] = part.levels[row]
        norms[index] = part.norms[row]
        sources.push({ ...part.sources[row], group })
    })
    const recordingBackgrounds: Record<string, BackgroundModel> = {}
    for (const part of parts) recordingBackgrounds[part.recordingId] = part.background
    return {
        vectors, levels, norms, sources,
        background: combineBackgrounds(parts.map((part) => part.background)),
        recordingBackgrounds,
    }
}

/** Dataset background without one recording, for leave-one-recording-out evaluation. */
export function backgroundWithout(set: ReferenceSet, recordingId: string | null): BackgroundModel {
    if (recordingId === null) return set.background
    const others = Object.entries(set.recordingBackgrounds).filter(([id]) => id !== recordingId).map(([, model]) => model)
    return others.length ? combineBackgrounds(others) : set.background
}

/** The newest-frame part of a reference pattern, back in dB above background. */
export function referenceExcess(set: ReferenceSet, row: number): Float32Array {
    const start = row * VECTOR_LENGTH + VECTOR_LENGTH - BAND_COUNT
    return Float32Array.from(set.vectors.subarray(start, start + BAND_COUNT), (value) => value * set.norms[row])
}

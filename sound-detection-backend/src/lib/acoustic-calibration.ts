import { VECTOR_LENGTH } from "./acoustic-features"
import type { ReferenceSet } from "./acoustic-matching"

/**
 * Self-check of a reference set, computed once when detection starts:
 * how similar the dataset's normal sounds are to its anomalies, and how
 * similar the anomaly occurrences are to each other.
 */
export type Calibration = {
    /** Threshold that separates the dataset's own normal sounds from its anomalies; null when nothing can be measured. */
    suggestedThreshold: number | null
    /** Normal patterns that were compared. */
    normalPatterns: number
    /** Anomaly occurrences (segments) that were compared with the other occurrences. */
    occurrences: number
    /** Share of occurrences recognised from the other occurrences at the suggested threshold. */
    recognisedAtSuggestion: number | null
    /** False when normal sounds resemble the anomaly as closely as its own occurrences do. */
    separable: boolean | null
    notes: string[]
}

const MAX_ROWS = 1500
/** Patterns this close in time in the same recording are near-duplicates, not evidence. */
const NEIGHBOUR_MS = 300
const MIN_SUGGESTION = 0.6
const MAX_SUGGESTION = 0.98

function evenly(rows: number[], maximum: number): number[] {
    if (rows.length <= maximum) return rows
    return Array.from({ length: maximum }, (_, index) => rows[Math.floor((index * rows.length) / maximum)])
}

function quantile(values: readonly number[], fraction: number): number {
    const sorted = [...values].sort((left, right) => left - right)
    const position = (sorted.length - 1) * fraction
    const low = Math.floor(position)
    const high = Math.ceil(position)
    return sorted[low] + (sorted[high] - sorted[low]) * (position - low)
}

const clampSuggestion = (value: number) =>
    Math.round(Math.min(MAX_SUGGESTION, Math.max(MIN_SUGGESTION, value)) * 100) / 100

export function calibrate(set: ReferenceSet): Calibration {
    const vectors = set.vectors
    const dot = (left: number, right: number) => {
        const a = left * VECTOR_LENGTH
        const b = right * VECTOR_LENGTH
        let total = 0
        for (let index = 0; index < VECTOR_LENGTH; index += 1) total += vectors[a + index] * vectors[b + index]
        return total
    }
    const usable = (row: number) => set.levels[row] > 0
    const anomalyRows = evenly(set.sources.flatMap((source, row) => source.label === "Anomaly" && usable(row) ? [row] : []), MAX_ROWS)
    const normalRows = evenly(set.sources.flatMap((source, row) => source.label === "Normal" && usable(row) ? [row] : []), MAX_ROWS)

    const normalScores: number[] = []
    for (const normal of normalRows) {
        const source = set.sources[normal]
        let best = -1
        for (const anomaly of anomalyRows) {
            const other = set.sources[anomaly]
            if (other.recordingId === source.recordingId && Math.abs(other.timeMs - source.timeMs) < NEIGHBOUR_MS) continue
            best = Math.max(best, dot(normal, anomaly))
        }
        if (best >= 0) normalScores.push(Math.min(1, best))
    }

    const groups = new Map<number, number[]>()
    for (const row of anomalyRows) {
        const group = set.sources[row].group
        if (group < 0) continue
        groups.set(group, [...(groups.get(group) ?? []), row])
    }
    const occurrenceScores: number[] = []
    if (groups.size >= 2) {
        for (const [group, rows] of groups) {
            let best = 0
            for (const row of rows) {
                for (const other of anomalyRows) {
                    if (set.sources[other].group !== group) best = Math.max(best, dot(row, other))
                }
            }
            occurrenceScores.push(Math.min(1, best))
        }
    }

    const normalBased = normalScores.length ? quantile(normalScores, 0.99) + 0.02 : null
    const occurrenceBased = occurrenceScores.length ? quantile(occurrenceScores, 0.1) - 0.02 : null
    let suggestedThreshold: number | null = null
    if (normalBased !== null && occurrenceBased !== null) {
        suggestedThreshold = clampSuggestion(normalBased <= occurrenceBased ? (normalBased + occurrenceBased) / 2 : normalBased)
    } else if (normalBased !== null) {
        suggestedThreshold = clampSuggestion(normalBased)
    } else if (occurrenceBased !== null) {
        suggestedThreshold = clampSuggestion(occurrenceBased)
    }
    const recognisedAtSuggestion = suggestedThreshold !== null && occurrenceScores.length
        ? occurrenceScores.filter((score) => score >= suggestedThreshold!).length / occurrenceScores.length
        : null
    const separable = recognisedAtSuggestion === null || normalBased === null ? null : recognisedAtSuggestion >= 0.5

    const notes: string[] = []
    if (normalRows.length === 0) {
        notes.push("Nothing outside the anomaly stands out from the background, so steady background noise cannot trigger detection. Record Normal examples of other loud sounds to calibrate against them.")
    }
    if (groups.size < 2) {
        notes.push("Annotate at least two occurrences of the anomaly to estimate how reliably it is recognised.")
    }
    if (separable !== false && normalBased !== null && normalBased > 0.92) {
        notes.push(`Some sounds outside the annotated segments look like the anomaly (${Math.round(Math.min(1, normalBased) * 100)}% similar). Check the recordings for unannotated occurrences.`)
    }
    if (separable === false) {
        notes.push("Some normal sounds resemble the anomaly as closely as its own occurrences do. Check the recordings for unannotated occurrences, or add more varied examples.")
    }
    return {
        suggestedThreshold, normalPatterns: normalScores.length, occurrences: occurrenceScores.length,
        recognisedAtSuggestion, separable, notes,
    }
}

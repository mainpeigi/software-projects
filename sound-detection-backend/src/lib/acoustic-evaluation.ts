import { FRAME_SECONDS, HOP_SECONDS, MATCH_HOP_SIZE, frameCountFor } from "./acoustic-features"
import { AcousticDetector, type DetectorSettings } from "./acoustic-detector"
import { AcousticMatchGate, DEFAULT_GATE_TIMING, type AcousticGateResult, type GateTiming } from "./acoustic-match-gate"
import {
    decide,
    emptyScore,
    matchFromScore,
    type AcousticMatch,
    type MatchSettings,
    type ReferenceMatcher,
    type ReferenceSet,
} from "./acoustic-matching"
import { backgroundWithout } from "./acoustic-references"
import type { TimeRange } from "./acoustic-recording-analysis"

/** Per-frame scores of one recording, independent of the threshold. */
export type ScoreTrack = {
    recordingId: string
    frameCount: number
    anomaly: Float32Array
    anomalyIndex: Int32Array
    normal: Float32Array
    level: Float32Array
}

export type Alarm = TimeRange & { match: AcousticMatch }

/** Frame times are frame centres, so alarms line up with the spectrogram. */
export const trackTimeSeconds = (frame: number) => frame * HOP_SECONDS + FRAME_SECONDS / 2

class AlarmCollector {
    readonly alarms: Alarm[] = []
    private open: Alarm | null = null

    add(gate: AcousticGateResult, matched: boolean, timeSeconds: number) {
        const timeMs = timeSeconds * 1000
        if (gate.anomaly && gate.match) {
            if (!this.open) {
                this.open = { startMs: (gate.startedAt ?? timeSeconds) * 1000, endMs: timeMs, match: gate.match }
            } else {
                this.open.match = gate.match
                if (matched) this.open.endMs = timeMs
            }
        } else if (this.open) {
            this.alarms.push(this.open)
            this.open = null
        }
    }

    finish(): Alarm[] {
        if (this.open) this.alarms.push(this.open)
        this.open = null
        return this.alarms
    }
}

/**
 * Runs the live pipeline over a whole recording. With `excludeRecordingId`
 * the recording's own patterns and background are left out, so the result
 * shows how it would be detected by the rest of the dataset.
 */
export function scoreRecording(matcher: ReferenceMatcher, settings: DetectorSettings, samples: Float32Array, {
    recordingId, excludeRecordingId = null, onProgress, timing = DEFAULT_GATE_TIMING,
}: {
    recordingId: string
    excludeRecordingId?: string | null
    onProgress?: (completed: number, total: number) => void
    timing?: GateTiming
}): { track: ScoreTrack; alarms: Alarm[] } {
    const frameCount = frameCountFor(samples.length)
    const detector = new AcousticDetector(matcher, settings, {
        excludeRecordingId, background: backgroundWithout(matcher.set, excludeRecordingId), timing,
    })
    const track: ScoreTrack = {
        recordingId, frameCount,
        anomaly: new Float32Array(frameCount), anomalyIndex: new Int32Array(frameCount),
        normal: new Float32Array(frameCount), level: new Float32Array(frameCount),
    }
    const collector = new AlarmCollector()
    for (let frame = 0; frame < frameCount; frame += 1) {
        const result = detector.processSamples(samples, frame * MATCH_HOP_SIZE, trackTimeSeconds(frame))
        track.anomaly[frame] = result.score.anomaly
        track.anomalyIndex[frame] = result.score.anomalyIndex
        track.normal[frame] = result.score.normal
        track.level[frame] = result.score.level
        collector.add(result.gate, result.decision !== null, result.timeSeconds)
        if (onProgress && frame % 512 === 511) onProgress(frame + 1, frameCount)
    }
    onProgress?.(frameCount, frameCount)
    return { track, alarms: collector.finish() }
}

/**
 * Re-decides stored scores under other settings. The background adaptation
 * of the original run is kept, so this is a close approximation of a full rerun.
 */
export function replayAlarms(track: ScoreTrack, set: ReferenceSet, settings: MatchSettings, timing = DEFAULT_GATE_TIMING): Alarm[] {
    const gate = new AcousticMatchGate(timing)
    const score = emptyScore()
    const collector = new AlarmCollector()
    for (let frame = 0; frame < track.frameCount; frame += 1) {
        score.anomaly = track.anomaly[frame]
        score.anomalyIndex = track.anomalyIndex[frame]
        score.normal = track.normal[frame]
        score.normalIndex = -1
        score.level = track.level[frame]
        const decision = decide(score, settings)
        const timeSeconds = trackTimeSeconds(frame)
        collector.add(gate.update(matchFromScore(score, decision, set), timeSeconds), decision !== null, timeSeconds)
    }
    return collector.finish()
}

export type TruthRecording = {
    recordingId: string
    label: "Normal" | "Anomaly"
    /** Only annotated Anomaly recordings have ground truth; Normal recordings have none by definition. */
    annotated: boolean
    durationMs: number
    segments: readonly TimeRange[]
}

export type EvaluationSummary = {
    recordings: number
    minutes: number
    occurrences: number
    detected: number
    recall: number | null
    alarms: number
    falseAlarms: number
    precision: number | null
    falseAlarmsPerMinute: number | null
}

/** Alarms may start up to this far before or after an annotated occurrence. */
export const MATCH_TOLERANCE_MS = 250

const overlaps = (left: TimeRange, right: TimeRange, tolerance: number) =>
    left.startMs <= right.endMs + tolerance && left.endMs >= right.startMs - tolerance

export const evaluable = (truth: TruthRecording) => truth.label === "Normal" || truth.annotated

export function summarize(items: readonly { truth: TruthRecording; alarms: readonly Alarm[] }[]): EvaluationSummary {
    let recordings = 0
    let durationMs = 0
    let occurrences = 0
    let detected = 0
    let alarms = 0
    let falseAlarms = 0
    for (const { truth, alarms: found } of items) {
        if (!evaluable(truth)) continue
        recordings += 1
        durationMs += truth.durationMs
        const segments = truth.label === "Anomaly" ? truth.segments : []
        occurrences += segments.length
        detected += segments.filter((segment) => found.some((alarm) => overlaps(alarm, segment, MATCH_TOLERANCE_MS))).length
        alarms += found.length
        falseAlarms += found.filter((alarm) => !segments.some((segment) => overlaps(alarm, segment, MATCH_TOLERANCE_MS))).length
    }
    const minutes = durationMs / 60_000
    return {
        recordings, minutes, occurrences, detected,
        recall: occurrences ? detected / occurrences : null,
        alarms, falseAlarms,
        precision: alarms ? (alarms - falseAlarms) / alarms : null,
        falseAlarmsPerMinute: minutes > 0 ? falseAlarms / minutes : null,
    }
}

export const SWEEP_THRESHOLDS: readonly number[] = Object.freeze(Array.from({ length: 20 }, (_, index) => Math.round((0.6 + index * 0.02) * 100) / 100))

export function sweepThresholds(
    items: readonly { truth: TruthRecording; track: ScoreTrack }[],
    set: ReferenceSet,
    settings: MatchSettings,
    thresholds: readonly number[] = SWEEP_THRESHOLDS,
): { threshold: number; summary: EvaluationSummary }[] {
    return thresholds.map((threshold) => ({
        threshold,
        summary: summarize(items.map(({ truth, track }) => ({
            truth, alarms: replayAlarms(track, set, { ...settings, threshold }),
        }))),
    }))
}

import { BAND_COUNT, FrameAnalyzer, VECTOR_LENGTH } from "./acoustic-features"
import { BackgroundTracker, type BackgroundModel } from "./acoustic-background"
import { AcousticMatchGate, DEFAULT_GATE_TIMING, type AcousticGateResult, type GateTiming } from "./acoustic-match-gate"
import {
    DEFAULT_MATCH_SETTINGS,
    ExcessHistory,
    decide,
    emptyScore,
    matchFromScore,
    validateMatchSettings,
    type Decision,
    type FrameScore,
    type MatchSettings,
    type ReferenceMatcher,
} from "./acoustic-matching"

export type DetectorSettings = MatchSettings & {
    /** Follow the live room instead of keeping the dataset background fixed. */
    adaptBackground: boolean
}

export const DEFAULT_DETECTOR_SETTINGS: Readonly<DetectorSettings> = Object.freeze({
    ...DEFAULT_MATCH_SETTINGS, adaptBackground: true,
})

export type FrameResult = {
    timeSeconds: number
    /** Reused between calls: copy the numbers you keep. */
    score: FrameScore
    decision: Decision
    gate: AcousticGateResult
}

/** Consecutive frames further apart than this lose their stacked context. */
const CONTEXT_GAP_SECONDS = 0.1

/**
 * One detection pipeline: background tracking, stacked excess patterns,
 * nearest-pattern scoring, decision and event gate. The live worker and the
 * offline evaluation run exactly this code.
 */
export class AcousticDetector {
    private readonly matcher: ReferenceMatcher
    private readonly excludeRecordingId: string | null
    private readonly timing: GateTiming
    private settings: DetectorSettings
    private tracker: BackgroundTracker
    private readonly history = new ExcessHistory()
    private readonly vector = new Float32Array(VECTOR_LENGTH)
    private readonly score = emptyScore()
    private readonly analyzer = new FrameAnalyzer()
    private readonly bands = new Float32Array(BAND_COUNT)
    private gate: AcousticMatchGate
    private previousTime: number | null = null
    /** The previous frame belonged to a known anomaly: neither background nor gain may learn from it. */
    private learningBlocked = false

    constructor(matcher: ReferenceMatcher, settings: DetectorSettings, {
        excludeRecordingId = null, background = matcher.set.background, timing = DEFAULT_GATE_TIMING,
    }: { excludeRecordingId?: string | null; background?: BackgroundModel; timing?: GateTiming } = {}) {
        this.matcher = matcher
        this.excludeRecordingId = excludeRecordingId
        this.timing = timing
        this.settings = { ...validateMatchSettings(settings), adaptBackground: settings.adaptBackground }
        this.tracker = new BackgroundTracker(background, { adapt: settings.adaptBackground })
        this.gate = new AcousticMatchGate(timing)
    }

    get background(): BackgroundModel { return this.tracker.model }
    get currentSettings(): DetectorSettings { return this.settings }

    /** Applies new settings. The background learned so far is kept; open events end. */
    configure(settings: DetectorSettings): void {
        const next = { ...validateMatchSettings(settings), adaptBackground: settings.adaptBackground }
        if (next.adaptBackground !== this.settings.adaptBackground) {
            this.tracker = new BackgroundTracker(this.tracker.model, { adapt: next.adaptBackground })
        }
        this.settings = next
        this.gate = new AcousticMatchGate(this.timing)
    }

    reset(): void {
        this.gate.reset()
        this.history.clear()
        this.previousTime = null
        this.learningBlocked = false
    }

    /** Gain offset between the live microphone and the dataset background (dB). */
    get gainOffsetDb(): number { return this.history.gainDb }

    /** Analyses one frame of samples starting at `offset`. */
    processSamples(samples: Float32Array, offset: number, timeSeconds: number): FrameResult {
        return this.processBands(this.bands, this.analyzer.analyze(samples, offset, this.bands), timeSeconds)
    }

    processBands(bands: ArrayLike<number>, rmsDb: number, timeSeconds: number): FrameResult {
        const score = this.score
        if (!Number.isFinite(rmsDb) || !Number.isFinite(timeSeconds)) {
            this.reset()
            score.anomaly = 0; score.anomalyIndex = -1; score.normal = 0; score.normalIndex = -1; score.level = 0
            return { timeSeconds, score, decision: null, gate: this.gate.update(null, Number.NaN) }
        }
        const dt = this.previousTime === null ? 0 : timeSeconds - this.previousTime
        if (dt < 0 || dt > CONTEXT_GAP_SECONDS) this.history.clear()
        this.previousTime = timeSeconds

        const level = this.history.push(bands, this.tracker.model, { dtSeconds: Math.max(0, dt), frozen: this.learningBlocked })
        this.history.vector(this.vector)
        this.matcher.score(this.vector, level, score, this.excludeRecordingId)
        const decision = decide(score, this.settings)
        const gate = this.gate.update(matchFromScore(score, decision, this.matcher.set), timeSeconds)
        this.learningBlocked = decision === "anomaly" || (gate.anomaly && gate.match?.kind !== "Unknown")
        this.tracker.update(bands, Math.max(0, dt), this.learningBlocked)
        return { timeSeconds, score, decision, gate }
    }

    /** Excess spectrum (dB) of the newest live frame. */
    liveExcess(): Float32Array {
        return Float32Array.from(this.history.current())
    }
}

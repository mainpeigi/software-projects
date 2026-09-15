import type { AcousticMatch } from "./acoustic-matching"

export type AcousticGateResult = {
    anomaly: boolean
    /** Strongest match of the active event. */
    match: AcousticMatch | null
    /** Increments per confirmed event; 0 while inactive. */
    eventId: number
    /** Audio-clock time the event's evidence started. */
    startedAt: number | null
}

export type GateTiming = {
    /** Sustained and unknown sounds must match for this long before an event opens. */
    attackSeconds: number
    /** Sustained events close after this long without a match. */
    releaseSeconds: number
    /** Impulsive events stay visible this long after their last match. */
    impulsiveHoldSeconds: number
    /** No new event may open this soon after one closes, so one snap is one event. */
    refractorySeconds: number
    /** Short dropouts tolerated while an attack is building up. */
    attackGapSeconds: number
}

export const DEFAULT_GATE_TIMING: Readonly<GateTiming> = Object.freeze({
    attackSeconds: 0.2,
    releaseSeconds: 0.5,
    impulsiveHoldSeconds: 0.5,
    refractorySeconds: 0.3,
    attackGapSeconds: 0.05,
})

const MAX_GAP_SECONDS = 0.3
const TIME_EPSILON = 1e-9

/**
 * Audio-clock hysteresis. Impulsive matches open an event on a single frame;
 * sustained and unknown matches need `attackSeconds` of evidence. Different
 * references may support the same event; the strongest one is reported.
 */
export class AcousticMatchGate {
    private readonly timing: GateTiming
    private previousTime: number | null = null
    private candidateSince: number | null = null
    private candidateBest: AcousticMatch | null = null
    private lastMatchTime: number | null = null
    private best: AcousticMatch | null = null
    private active = false
    private startedAt: number | null = null
    private refractoryUntil = -Infinity
    private events = 0

    constructor(timing: GateTiming = DEFAULT_GATE_TIMING) {
        this.timing = timing
    }

    /** True while an event is open or building up: the background must not learn it. */
    get engaged(): boolean {
        return this.active || this.candidateSince !== null
    }

    reset(): void {
        this.previousTime = null
        this.candidateSince = null
        this.candidateBest = null
        this.lastMatchTime = null
        this.best = null
        this.active = false
        this.startedAt = null
        this.refractoryUntil = -Infinity
    }

    update(match: AcousticMatch | null, timeSeconds: number): AcousticGateResult {
        if (!Number.isFinite(timeSeconds)) {
            this.reset()
            return this.result()
        }
        if (this.previousTime !== null && (
            timeSeconds < this.previousTime - TIME_EPSILON ||
            timeSeconds - this.previousTime > MAX_GAP_SECONDS + TIME_EPSILON
        )) {
            this.reset()
        }
        this.previousTime = timeSeconds

        if (match) {
            this.lastMatchTime = timeSeconds
            if (this.active) {
                if (!this.best || match.similarity > this.best.similarity) this.best = match
                return this.result()
            }
            if (timeSeconds < this.refractoryUntil - TIME_EPSILON) return this.result()
            this.candidateSince ??= timeSeconds
            if (!this.candidateBest || match.similarity > this.candidateBest.similarity) this.candidateBest = match
            if (match.kind === "Impulsive" ||
                timeSeconds - this.candidateSince + TIME_EPSILON >= this.timing.attackSeconds) {
                this.active = true
                this.best = this.candidateBest
                this.startedAt = this.candidateSince
                this.events += 1
                this.candidateSince = null
                this.candidateBest = null
            }
            return this.result()
        }

        if (!this.active) {
            if (this.candidateSince !== null && (this.lastMatchTime === null ||
                timeSeconds - this.lastMatchTime > this.timing.attackGapSeconds + TIME_EPSILON)) {
                this.candidateSince = null
                this.candidateBest = null
            }
            return this.result()
        }

        const hold = this.best?.kind === "Impulsive" ? this.timing.impulsiveHoldSeconds : this.timing.releaseSeconds
        if (this.lastMatchTime === null || timeSeconds - this.lastMatchTime + TIME_EPSILON >= hold) {
            this.active = false
            this.best = null
            this.startedAt = null
            this.refractoryUntil = timeSeconds + this.timing.refractorySeconds
        }
        return this.result()
    }

    private result(): AcousticGateResult {
        return this.active
            ? { anomaly: true, match: this.best, eventId: this.events, startedAt: this.startedAt }
            : { anomaly: false, match: null, eventId: 0, startedAt: null }
    }
}

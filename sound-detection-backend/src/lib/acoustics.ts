export const MAX_HZ = 20_000

export type Peak = {
    hz: number
    level: number
    prominence: number
    harmonic: number | null
}

export type Band = {
    center: number
    from: number
    to: number
    level: number
    weighted: number
    bins: number
}

export type TimeMetrics = { 
    rms: number
    peak: number
    crest: number
}

export function toAmplitude(dbfs: number) {
    return Math.min(100, Math.max(0, dbfs + 100))
}

export function hzPerBin(sampleRate: number, binCount: number) {
    return sampleRate / 2 / binCount
}

function median(values: number[]) {
    if (values.length === 0) return -100
    const sorted = [...values].sort((a, b) => a - b)
    return sorted[Math.floor(sorted.length / 2)]
}

export function noiseFloor(bins: Float32Array, limit: number) {
    const finite: number[] = []
    for (let index = 1; index < limit; index += 1) {
        if (Number.isFinite(bins[index])) finite.push(bins[index])
    }
    return median(finite)
}

export function findPeaks(bins: Float32Array, sampleRate: number, { minProminence = 12, maxCount = 8, mergeHz = 120, minHz = 40 } = {},) : Peak[] {
    const perBin = hzPerBin(sampleRate, bins.length)
    const limit = Math.min(bins.length, Math.floor(MAX_HZ / perBin))
    const floor = noiseFloor(bins, limit)

    const candidates: Peak[] = []
    for (let index = Math.max(1, Math.ceil(minHz / perBin)); index < limit - 1; index += 1) {
        const value = bins[index]
        if (!Number.isFinite(value)) continue
        if (value <= bins[index - 1] || value < bins[index + 1]) continue
        const prominence = value - floor
        if(prominence < minProminence) continue
        candidates.push({ hz: index * perBin, level: toAmplitude(value), prominence, harmonic: null })
    }

    candidates.sort((a, b) => b.prominence - a.prominence)

    const kept: Peak[] = []
    for (const candidate of candidates) {
        if (kept.length >= maxCount) break
        if (kept.some((peak) => Math.abs(peak.hz - candidate.hz) < mergeHz)) continue
        kept.push(candidate)
    }

    return markHarmonics(kept)
}

function markHarmonics(peaks: Peak[], tolerance = 0.06): Peak[] {
    if (peaks.length === 0) return peaks
    const fundamental = peaks[0].hz
    if (fundamental <= 0) return peaks
    return peaks.map((peak, position) => {
        if (position === 0) return peak
        const ratio = peak.hz / fundamental
        const nearest = Math.round(ratio)
        if (nearest < 2 || Math.abs(ratio - nearest) > tolerance) return peak
        return { ...peak, harmonic: nearest }
    })
}

const THIRD_OCTAVE_CENTERS = [25, 31.5, 40, 50, 63, 80, 100, 125, 160, 200, 250, 315, 400, 500, 630, 800, 1000, 1250, 1600, 2000, 2500, 3150, 4000, 5000, 6300, 8000, 10_000, 12_500, 16_000, 20_000,]

const SIXTH_OCTAVE = 2 ** (1 / 6)

export function aWeighting(hz: number) {
    if (hz <= 0) return -Infinity
    const f2 = hz * hz
    const numerator = 12194 ** 2 * f2 * f2
    const denominator = (f2 + 20.6 ** 2) * Math.sqrt((f2 + 107.7 ** 2) * (f2 + 737.9 ** 2)) * (f2 + 12194 ** 2)
    return 20 * Math.log10(numerator / denominator) + 2.0
}

export function thirdOctaveBands(bins: Float32Array, sampleRate: number): Band[] {
    const perBin = hzPerBin(sampleRate, bins.length)
    const limit = Math.min(bins.length, Math.floor(MAX_HZ / perBin))

    return THIRD_OCTAVE_CENTERS.map((center) => {
        const from = center / SIXTH_OCTAVE
        const to = center * SIXTH_OCTAVE
        const first = Math.max(1, Math.ceil(from / perBin))
        const last = Math.min(limit - 1, Math.floor(to / perBin))
        
        let sum = 0
        let count = 0
        for (let index = first; index <= last; index += 1) {
            if (!Number.isFinite(bins[index])) continue
            sum += bins[index]
            count += 1
        }

        const level = count ? toAmplitude(sum / count) : 0
        return { center, from, to, level, weighted: count ? level + aWeighting(center) : 0, bins : count }
    })
}

export function timeMetrics(samples: Float32Array): TimeMetrics {
    let sumSquares = 0
    let peak = 0
    for (let index = 0; index < samples.length; index += 1) {
        const value = samples[index]
        sumSquares += value * value
        const magnitude = Math.abs(value)
        if (magnitude > peak) peak = magnitude
    }
    const rms = Math.sqrt(sumSquares / Math.max(1, samples.length))
    const toDb = (value: number) => (value > 0 ? 20 * Math.log10(value) : -100)
    return {
        rms: toAmplitude(toDb(rms)),
        peak: toAmplitude(toDb(peak)),
        crest: rms > 0 ? 20 * Math.log10(peak / rms) : 0,
    }
}

export function formatHz(hz: number) {
    return hz >= 1000 ? `${(hz / 1000).toFixed(1)} kHz` : `${Math.round(hz)} Hz`
}
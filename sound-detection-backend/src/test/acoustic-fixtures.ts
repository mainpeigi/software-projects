import { FRAME_SECONDS, MATCH_HOP_SIZE, frameCountFor } from "@/lib/acoustic-features"
import type { AcousticDetector } from "@/lib/acoustic-detector"
import type { ReferenceLabel, ReferenceSet } from "@/lib/acoustic-matching"
import { analyzeRecordingSync } from "@/lib/acoustic-recording-analysis"
import { buildReferenceSet, extractRecordingReferences, type ExtractionSegment } from "@/lib/acoustic-references"
import { addRattle, addSnap, background, RATE } from "./synthetic-audio"

export type FixtureRecording = { id: string; label: ReferenceLabel; samples: Float32Array; segments: ExtractionSegment[] }

/** Three seconds of room noise with snaps at 0.8 s and 1.9 s, both annotated. */
export function snapRecording(id = "snaps", { annotated = true } = {}): FixtureRecording {
  const samples = background(3, { seed: 1 })
  addSnap(samples, 0.8, { seed: 3 })
  addSnap(samples, 1.9, { seed: 4 })
  return {
    id, label: "Anomaly", samples,
    segments: annotated ? [
      { id: `${id}-1`, startMs: 795, endMs: 835, kind: "Auto" },
      { id: `${id}-2`, startMs: 1895, endMs: 1935, kind: "Auto" },
    ] : [],
  }
}

/** Four seconds with a rattle from 1.0 s to 2.5 s, annotated as one sustained segment. */
export function rattleRecording(id = "rattle"): FixtureRecording {
  const samples = background(4, { seed: 5 })
  addRattle(samples, 1, 1.5, { seed: 6 })
  return { id, label: "Anomaly", samples, segments: [{ id: `${id}-1`, startMs: 1000, endMs: 2500, kind: "Auto" }] }
}

export function referencesFor(recordings: readonly FixtureRecording[], maximum?: number): ReferenceSet {
  return buildReferenceSet(recordings.map((recording) => extractRecordingReferences(
    analyzeRecordingSync(recording.samples, { display: false }), recording, recording.segments)), maximum)
}

export type ObservedAlarm = { start: number; end: number; kind: string; eventId: number }

/** Runs a detector over a whole clip, as the live worker would, and collects gate events. */
export function collectAlarms(detector: AcousticDetector, samples: Float32Array): ObservedAlarm[] {
  const alarms: ObservedAlarm[] = []
  let open: ObservedAlarm | null = null
  for (let frame = 0; frame < frameCountFor(samples.length); frame += 1) {
    const time = (frame * MATCH_HOP_SIZE) / RATE + FRAME_SECONDS
    const { gate } = detector.processSamples(samples, frame * MATCH_HOP_SIZE, time)
    if (gate.anomaly && (!open || open.eventId !== gate.eventId)) {
      open = { start: time, end: time, kind: gate.match?.kind ?? "", eventId: gate.eventId }
      alarms.push(open)
    } else if (gate.anomaly && open) {
      open.end = time
    } else if (!gate.anomaly) {
      open = null
    }
  }
  return alarms
}

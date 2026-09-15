import { randomUUID } from "node:crypto"
import { ApiError } from "./errors"
import { FileStore } from "./storage"
import { decodeWav } from "./lib/wav"
import { MATCH_FRAME_SIZE, MATCH_SAMPLE_RATE } from "./lib/acoustic-features"
import { analyzeRecordingSync } from "./lib/acoustic-recording-analysis"
import { buildReferenceSet, extractRecordingReferences } from "./lib/acoustic-references"
import { ReferenceMatcher, validateMatchSettings } from "./lib/acoustic-matching"
import { DEFAULT_DETECTOR_SETTINGS, type DetectorSettings } from "./lib/acoustic-detector"
import { scoreRecording } from "./lib/acoustic-evaluation"
import { calibrate } from "./lib/acoustic-calibration"

export const MAX_AUDIO_BYTES = 64 * 1024 * 1024

export function audioSamples(audio: Buffer): Float32Array {
  if (!audio.length || audio.length > MAX_AUDIO_BYTES) throw new ApiError(413, "Use a non-empty WAV of at most 64 MiB.")
  let decoded
  try { decoded = decodeWav(audio.buffer.slice(audio.byteOffset, audio.byteOffset + audio.byteLength) as ArrayBuffer) }
  catch { throw new ApiError(400, "Upload a PCM or float WAV file.") }
  if (decoded.sampleRate !== MATCH_SAMPLE_RATE) throw new ApiError(400, "Convert the WAV to 48000 Hz before uploading.")
  const samples = decoded.samples
  if (samples.length < MATCH_FRAME_SIZE || samples.length > 600 * MATCH_SAMPLE_RATE) throw new ApiError(400, "Audio must contain at least 2048 samples and be at most 10 minutes long.")
  for (const sample of samples) if (!Number.isFinite(sample) || sample < -1 || sample > 1) throw new ApiError(400, "Audio samples must be finite values between -1 and 1.")
  return samples
}

export async function addReference(store: FileStore, datasetId: string, audio: Buffer, label: unknown, note: unknown = "") {
  if (label !== "Normal" && label !== "Anomaly") throw new ApiError(400, "The recording label must be Normal or Anomaly.")
  if (typeof note !== "string" || note.length > 1000) throw new ApiError(400, "The note must be at most 1000 characters.")
  const samples = audioSamples(audio)
  return store.addRecording(datasetId, audio, { label, note, durationMs: samples.length / MATCH_SAMPLE_RATE * 1000 })
}

export async function detect(store: FileStore, datasetId: string, audio: Buffer, options: Partial<DetectorSettings> = {}) {
  const samples = audioSamples(audio)
  const settings = { ...DEFAULT_DETECTOR_SETTINGS, ...options }
  try { validateMatchSettings(settings) }
  catch (error) { throw new ApiError(400, (error as Error).message) }
  if (typeof settings.adaptBackground !== "boolean") throw new ApiError(400, "adaptBackground must be true or false.")
  const recordings = await store.listRecordings(datasetId)
  if (!recordings.some(recording => recording.label === "Anomaly")) throw new ApiError(400, "Add at least one Anomaly reference recording before detection.")
  const parts = []
  for (const recording of recordings) {
    const reference = audioSamples(await store.audio(datasetId, recording.id))
    parts.push(extractRecordingReferences(analyzeRecordingSync(reference, { display: false }), recording, recording.segments))
  }
  let references
  try { references = buildReferenceSet(parts) }
  catch (error) { throw new ApiError(400, (error as Error).message) }
  if (!references.sources.some(source => source.label === "Anomaly")) throw new ApiError(400, "No reference sound stands out from the background. Annotate its segment or record it more clearly.")
  const id = randomUUID()
  const { track, alarms } = scoreRecording(new ReferenceMatcher(references), settings, samples, { recordingId: id })
  return store.saveEvent(datasetId, {
    id, datasetId, createdAt: new Date().toISOString(), sampleRate: MATCH_SAMPLE_RATE,
    durationMs: samples.length / MATCH_SAMPLE_RATE * 1000, processedFrames: track.frameCount,
    settings, calibration: calibrate(references), alarms,
  })
}

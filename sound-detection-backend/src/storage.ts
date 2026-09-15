import { randomUUID } from "node:crypto"
import { mkdir, readFile, readdir, rename, unlink, writeFile } from "node:fs/promises"
import path from "node:path"
import { ApiError, requireId } from "./errors"
import type { ExtractionSegment } from "./lib/acoustic-references"

export type Dataset = { id: string; name: string; createdAt: string }
export type Recording = {
  id: string; datasetId: string; label: "Normal" | "Anomaly"; note: string;
  durationMs: number; sampleRate: 48000; createdAt: string; segments: ExtractionSegment[];
}

const isMissing = (error: unknown) => (error as NodeJS.ErrnoException).code === "ENOENT"

async function readJson<T>(file: string): Promise<T> {
  try { return JSON.parse(await readFile(file, "utf8")) as T }
  catch (error) { if (isMissing(error)) throw new ApiError(404, "The requested item does not exist."); throw error }
}

async function atomicJson(file: string, value: unknown) {
  const temporary = `${file}.${randomUUID()}.tmp`
  try {
    await writeFile(temporary, JSON.stringify(value, null, 2) + "\n", { flag: "wx" })
    await rename(temporary, file)
  } finally {
    await unlink(temporary).catch(error => { if (!isMissing(error)) throw error })
  }
}

async function entries(folder: string) {
  try { return await readdir(folder, { withFileTypes: true }) }
  catch (error) { if (isMissing(error)) return []; throw error }
}

/** Each item has its own file, so simultaneous uploads never overwrite a shared index. */
export class FileStore {
  readonly root: string
  constructor(root: string) { this.root = path.resolve(root) }

  private datasetPath(id: string) { return path.join(this.root, "datasets", requireId(id)) }
  private recordingPath(datasetId: string, id: string) { return path.join(this.datasetPath(datasetId), "recordings", requireId(id)) }

  async createDataset(name: unknown): Promise<Dataset> {
    if (typeof name !== "string" || !name.trim() || name.trim().length > 120) throw new ApiError(400, "Use a dataset name between 1 and 120 characters.")
    const dataset = { id: randomUUID(), name: name.trim(), createdAt: new Date().toISOString() }
    const folder = this.datasetPath(dataset.id)
    await mkdir(path.join(folder, "recordings"), { recursive: true })
    await mkdir(path.join(folder, "events"), { recursive: true })
    await atomicJson(path.join(folder, "metadata.json"), dataset)
    return dataset
  }

  getDataset(id: string) { return readJson<Dataset>(path.join(this.datasetPath(id), "metadata.json")) }

  async listDatasets(): Promise<Dataset[]> {
    const result: Dataset[] = []
    for (const entry of await entries(path.join(this.root, "datasets"))) {
      if (!entry.isDirectory() || !/^[0-9a-f-]{36}$/i.test(entry.name)) continue
      try { result.push(await this.getDataset(entry.name)) }
      catch (error) { if (!(error instanceof ApiError && error.status === 404)) throw error }
    }
    return result.sort((a, b) => a.createdAt.localeCompare(b.createdAt))
  }

  async addRecording(datasetId: string, audio: Buffer, metadata: Pick<Recording, "label" | "note" | "durationMs">): Promise<Recording> {
    await this.getDataset(datasetId)
    const recording: Recording = { ...metadata, id: randomUUID(), datasetId, sampleRate: 48000, createdAt: new Date().toISOString(), segments: [] }
    const folder = this.recordingPath(datasetId, recording.id)
    await mkdir(folder)
    await writeFile(path.join(folder, "audio.wav"), audio, { flag: "wx" })
    // Metadata is the commit marker: incomplete uploads are not listed.
    await atomicJson(path.join(folder, "metadata.json"), recording)
    return recording
  }

  async getRecording(datasetId: string, id: string): Promise<Recording> {
    await this.getDataset(datasetId)
    return readJson<Recording>(path.join(this.recordingPath(datasetId, id), "metadata.json"))
  }

  async audio(datasetId: string, id: string): Promise<Buffer> {
    await this.getRecording(datasetId, id)
    return readFile(path.join(this.recordingPath(datasetId, id), "audio.wav"))
  }

  async listRecordings(datasetId: string): Promise<Recording[]> {
    await this.getDataset(datasetId)
    const result: Recording[] = []
    for (const entry of await entries(path.join(this.datasetPath(datasetId), "recordings"))) {
      if (!entry.isDirectory() || !/^[0-9a-f-]{36}$/i.test(entry.name)) continue
      try { result.push(await this.getRecording(datasetId, entry.name)) }
      catch (error) { if (!(error instanceof ApiError && error.status === 404)) throw error }
    }
    return result.sort((a, b) => a.createdAt.localeCompare(b.createdAt))
  }

  async replaceSegments(datasetId: string, id: string, input: unknown) {
    const recording = await this.getRecording(datasetId, id)
    if (!Array.isArray(input) || input.length > 200) throw new ApiError(400, "Provide an array of at most 200 segments.")
    const segments: ExtractionSegment[] = input.map(value => {
      if (!value || !Number.isFinite(value.startMs) || !Number.isFinite(value.endMs) || value.startMs < 0 ||
        value.endMs > recording.durationMs || value.endMs - value.startMs < 20 || !["Auto", "Impulsive", "Sustained"].includes(value.kind)) {
        throw new ApiError(400, "Segments must be within the recording, at least 20 ms long, with kind Auto, Impulsive or Sustained.")
      }
      return { id: randomUUID(), startMs: value.startMs, endMs: value.endMs, kind: value.kind }
    })
    const updated = { ...recording, segments }
    await atomicJson(path.join(this.recordingPath(datasetId, id), "metadata.json"), updated)
    return segments
  }

  async saveEvent<T extends { id: string }>(datasetId: string, result: T): Promise<T> {
    await this.getDataset(datasetId)
    await atomicJson(path.join(this.datasetPath(datasetId), "events", `${requireId(result.id)}.json`), result)
    return result
  }

  async listEvents(datasetId: string): Promise<unknown[]> {
    await this.getDataset(datasetId)
    const result: unknown[] = []
    const folder = path.join(this.datasetPath(datasetId), "events")
    for (const entry of await entries(folder)) {
      if (entry.isFile() && /^[0-9a-f-]{36}\.json$/i.test(entry.name)) result.push(await readJson(path.join(folder, entry.name)))
    }
    return result
  }
}

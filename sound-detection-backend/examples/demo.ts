import path from "node:path"
import { mkdir, writeFile } from "node:fs/promises"
import { FileStore } from "../src/storage"
import { addReference, detect } from "../src/detection"
import { snapRecording } from "../src/test/acoustic-fixtures"
import { addSnap, background } from "../src/test/synthetic-audio"

function encodeWav(samples: Float32Array): Buffer {
  const output = Buffer.alloc(44 + samples.length * 2)
  output.write("RIFF", 0); output.writeUInt32LE(output.length - 8, 4)
  output.write("WAVEfmt ", 8); output.writeUInt32LE(16, 16)
  output.writeUInt16LE(1, 20); output.writeUInt16LE(1, 22)
  output.writeUInt32LE(48000, 24); output.writeUInt32LE(96000, 28)
  output.writeUInt16LE(2, 32); output.writeUInt16LE(16, 34)
  output.write("data", 36); output.writeUInt32LE(samples.length * 2, 40)
  samples.forEach((sample, index) => output.writeInt16LE(Math.round(Math.max(-1, Math.min(1, sample)) * 32767), 44 + index * 2))
  return output
}

const root = path.resolve(process.env.DATA_DIR || "data")
const store = new FileStore(root)
const dataset = await store.createDataset("Demo sound detection")
const reference = snapRecording()
const recording = await addReference(store, dataset.id, encodeWav(reference.samples), "Anomaly", "Synthetic reference with two snaps")
await store.replaceSegments(dataset.id, recording.id, reference.segments)
const input = background(4, { seed: 61 })
addSnap(input, 1.2, { seed: 62 }); addSnap(input, 2.8, { seed: 63 })
const wav = encodeWav(input)
await mkdir(root, { recursive: true })
await writeFile(path.join(root, "demo-input.wav"), wav)
const result = await detect(store, dataset.id, wav)
if (result.alarms.length !== 2) throw new Error(`Expected two sound occurrences, got ${result.alarms.length}.`)
console.log(JSON.stringify({
  datasetId: dataset.id, input: path.join(root, "demo-input.wav"),
  detectedSounds: result.alarms.length, alarms: result.alarms,
}, null, 2))

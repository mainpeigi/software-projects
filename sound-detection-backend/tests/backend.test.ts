import { afterEach, expect, test } from "vitest"
import { Server } from "node:http"
import { mkdtemp, rm } from "node:fs/promises"
import { tmpdir } from "node:os"
import path from "node:path"
import { createApp } from "../src/http"
import { snapRecording } from "../src/test/acoustic-fixtures"
import { background, addSnap } from "../src/test/synthetic-audio"

const roots: string[] = []
const servers: Server[] = []

// Real PCM WAV payloads exercise decoding, reference extraction and HTTP uploads.
function wav(samples: Float32Array, rate = 48000): Uint8Array<ArrayBuffer> {
  const bytes = new Uint8Array(44 + samples.length * 2)
  const view = new DataView(bytes.buffer)
  const text = (offset: number, value: string) => [...value].forEach((c, i) => view.setUint8(offset + i, c.charCodeAt(0)))
  text(0, "RIFF"); view.setUint32(4, bytes.length - 8, true); text(8, "WAVEfmt ")
  view.setUint32(16, 16, true); view.setUint16(20, 1, true); view.setUint16(22, 1, true)
  view.setUint32(24, rate, true); view.setUint32(28, rate * 2, true)
  view.setUint16(32, 2, true); view.setUint16(34, 16, true)
  text(36, "data"); view.setUint32(40, samples.length * 2, true)
  samples.forEach((sample, i) => view.setInt16(44 + i * 2, Math.round(Math.max(-1, Math.min(1, sample)) * 32767), true))
  return bytes
}

async function start(root?: string) {
  if (!root) {
    root = await mkdtemp(path.join(tmpdir(), "sound-backend-tests-"))
    roots.push(root)
  }
  const server = createApp({ dataDir: root })
  expect(server).toBeInstanceOf(Server)
  servers.push(server)
  await new Promise<void>(resolve => server.listen(0, "127.0.0.1", resolve))
  const address = server.address()
  if (!address || typeof address === "string") throw new Error("No listener")
  const base = `http://127.0.0.1:${address.port}`
  return { server, root, request: (url: string, init?: RequestInit) => fetch(base + url, init) }
}

afterEach(async () => {
  for (const server of servers.splice(0)) {
    server.closeAllConnections()
    if (server.listening) await new Promise<void>((resolve, reject) => server.close(error => error ? reject(error) : resolve()))
  }
  for (const root of roots.splice(0)) {
    if (path.dirname(path.resolve(root)) !== path.resolve(tmpdir()) || !path.basename(root).startsWith("sound-backend-tests-")) throw new Error("Unsafe test cleanup")
    await rm(root, { recursive: true, force: true })
  }
})

const jsonPost = (body: unknown): RequestInit => ({ method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) })

test("starts with an empty local folder and no database configuration", async () => {
  const app = await start()
  expect(await (await app.request("/health")).json()).toMatchObject({ status: "ok", storage: "files", database: false })
  expect(await (await app.request("/api/datasets")).json()).toEqual([])
})

test("persists datasets and recordings across a new server instance", async () => {
  const app = await start()
  const created = await app.request("/api/datasets", jsonPost({ name: "My sounds" }))
  expect(created.status).toBe(201)
  const dataset = await created.json()
  const bytes = wav(background(0.2))
  const uploaded = await app.request(`/api/datasets/${dataset.id}/recordings?label=Normal`, { method: "POST", body: bytes })
  expect(uploaded.status).toBe(201)
  const recording = await uploaded.json()
  await new Promise<void>(resolve => app.server.close(() => resolve()))
  const restarted = await start(app.root)
  expect(await (await restarted.request("/api/datasets")).json()).toMatchObject([{ id: dataset.id, name: "My sounds" }])
  expect(await (await restarted.request(`/api/datasets/${dataset.id}/recordings`)).json()).toMatchObject([{ id: recording.id, label: "Normal" }])
  const audio = await restarted.request(`/api/datasets/${dataset.id}/recordings/${recording.id}/audio`)
  expect(new Uint8Array(await audio.arrayBuffer())).toEqual(bytes)
})

test("detects two sound occurrences from uploaded references and saves the result", async () => {
  const app = await start()
  const dataset = await (await app.request("/api/datasets", jsonPost({ name: "Snaps" }))).json()
  const reference = snapRecording()
  const uploaded = await app.request(`/api/datasets/${dataset.id}/recordings?label=Anomaly`, { method: "POST", body: wav(reference.samples) })
  expect(uploaded.status).toBe(201)
  const recording = await uploaded.json()
  const segments = await app.request(`/api/datasets/${dataset.id}/recordings/${recording.id}/segments`, {
    ...jsonPost(reference.segments), method: "PUT",
  })
  expect(segments.status).toBe(200)
  const live = background(4, { seed: 61 })
  addSnap(live, 1.2, { seed: 62 }); addSnap(live, 2.8, { seed: 63 })
  const response = await app.request(`/api/datasets/${dataset.id}/detect`, { method: "POST", body: wav(live) })
  expect(response.status).toBe(200)
  const result = await response.json()
  expect(result.alarms).toHaveLength(2)
  expect(result.alarms[0].startMs).toBeGreaterThan(1150)
  expect(result.alarms[0].startMs).toBeLessThan(1300)
  expect(result.alarms[1].startMs).toBeGreaterThan(2750)
  expect(result.alarms[1].startMs).toBeLessThan(2900)
  expect(result.alarms[0].match.recordingId).toBe(recording.id)
  expect(await (await app.request(`/api/datasets/${dataset.id}/events`)).json()).toMatchObject([{ id: result.id, alarms: result.alarms }])
})

test("rejects invalid WAV, sample rates, labels and segments without saving invalid data", async () => {
  const app = await start()
  const dataset = await (await app.request("/api/datasets", jsonPost({ name: "Validation" }))).json()
  const url = `/api/datasets/${dataset.id}/recordings`
  expect((await app.request(`${url}?label=Anomaly`, { method: "POST", body: "not audio" })).status).toBe(400)
  expect((await app.request(`${url}?label=Anomaly`, { method: "POST", body: wav(background(0.1), 44100) })).status).toBe(400)
  expect((await app.request(`${url}?label=Wrong`, { method: "POST", body: wav(background(0.1)) })).status).toBe(400)
  expect(await (await app.request(url)).json()).toEqual([])
  const recording = await (await app.request(`${url}?label=Anomaly`, { method: "POST", body: wav(background(0.1)) })).json()
  expect((await app.request(`${url}/${recording.id}/segments`, { ...jsonPost([{ startMs: 0, endMs: 5000, kind: "Auto" }]), method: "PUT" })).status).toBe(400)
})

test("reports missing references, malformed JSON and missing datasets", async () => {
  const app = await start()
  expect((await app.request("/api/datasets", { method: "POST", body: "{" })).status).toBe(400)
  expect((await app.request("/api/datasets/not-an-id/recordings")).status).toBe(400)
  expect((await app.request("/api/datasets/11111111-1111-4111-8111-111111111111/recordings")).status).toBe(404)
  const dataset = await (await app.request("/api/datasets", jsonPost({ name: "Empty" }))).json()
  expect((await app.request(`/api/datasets/${dataset.id}/detect`, { method: "POST", body: wav(background(0.2)) })).status).toBe(400)
})

test("keeps multiple simultaneous uploads and rejects unapproved browser origins", async () => {
  const app = await start()
  const dataset = await (await app.request("/api/datasets", jsonPost({ name: "Concurrent" }))).json()
  const route = `/api/datasets/${dataset.id}/recordings?label=Normal`
  const responses = await Promise.all(Array.from({ length: 3 }, () => app.request(route, { method: "POST", body: wav(background(0.1)) })))
  expect(responses.map(response => response.status)).toEqual([201, 201, 201])
  expect(await (await app.request(`/api/datasets/${dataset.id}/recordings`)).json()).toHaveLength(3)
  expect((await app.request("/api/datasets", { ...jsonPost({ name: "Remote page" }), headers: { Origin: "https://example.org" } })).status).toBe(403)
})

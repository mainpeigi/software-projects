import { createServer, type IncomingMessage, type Server, type ServerResponse } from "node:http"
import { FileStore } from "./storage"
import { addReference, detect, MAX_AUDIO_BYTES } from "./detection"
import { ApiError } from "./errors"
import type { DetectorSettings } from "./lib/acoustic-detector"

async function body(request: IncomingMessage, maximum = MAX_AUDIO_BYTES): Promise<Buffer> {
  if (Number(request.headers["content-length"]) > maximum) throw new ApiError(413, "Request body too large.")
  const chunks: Buffer[] = []
  let length = 0
  for await (const chunk of request) {
    const buffer = Buffer.from(chunk)
    length += buffer.length
    if (length > maximum) throw new ApiError(413, "Request body too large.")
    chunks.push(buffer)
  }
  return Buffer.concat(chunks, length)
}

async function json(request: IncomingMessage): Promise<unknown> {
  try { return JSON.parse((await body(request, 128 * 1024)).toString("utf8")) }
  catch (error) { if (error instanceof ApiError) throw error; throw new ApiError(400, "Invalid JSON body.") }
}

function send(response: ServerResponse, status: number, value: unknown) {
  response.writeHead(status, { "Content-Type": "application/json; charset=utf-8", "Cache-Control": "no-store" })
  response.end(JSON.stringify(value))
}

function settings(query: URLSearchParams): Partial<DetectorSettings> {
  const result: Partial<DetectorSettings> = {}
  for (const key of ["threshold", "minMargin", "minLevelDb", "unknownBelow"] as const) {
    const raw = query.get(key)
    if (raw !== null) {
      if (!raw.trim() || !Number.isFinite(Number(raw))) throw new ApiError(400, `${key} must be a finite number.`)
      result[key] = Number(raw)
    }
  }
  const adapt = query.get("adaptBackground")
  if (adapt !== null) {
    if (adapt !== "true" && adapt !== "false") throw new ApiError(400, "adaptBackground must be true or false.")
    result.adaptBackground = adapt === "true"
  }
  return result
}

export function createApp({ dataDir, allowedOrigins = ["http://localhost:5173", "http://127.0.0.1:5173"] }: {
  dataDir: string; allowedOrigins?: string[];
}): Server {
  const store = new FileStore(dataDir)
  return createServer(async (request, response) => {
    try {
      const base = new URL(`http://${request.headers.host || "127.0.0.1"}`)
      if (!["localhost", "127.0.0.1", "[::1]"].includes(base.hostname)) throw new ApiError(403, "Use the local server address.")
      const origin = request.headers.origin
      if (origin) {
        if (origin !== base.origin && !allowedOrigins.includes(origin)) throw new ApiError(403, "Browser origin is not allowed.")
        response.setHeader("Access-Control-Allow-Origin", origin)
        response.setHeader("Vary", "Origin")
      }
      if (request.method === "OPTIONS") {
        response.writeHead(204, { "Access-Control-Allow-Methods": "GET, POST, PUT, OPTIONS", "Access-Control-Allow-Headers": "Content-Type" })
        response.end(); return
      }
      const url = new URL(request.url || "/", base)
      const parts = url.pathname.split("/").filter(Boolean)
      const method = request.method
      if (method === "GET" && url.pathname === "/health") {
        send(response, 200, { status: "ok", storage: "files", database: false }); return
      }
      if (method === "GET" && url.pathname === "/") {
        send(response, 200, { name: "Sound Detection Backend", health: "/health", datasets: "/api/datasets" }); return
      }
      if (parts[0] !== "api" || parts[1] !== "datasets") throw new ApiError(404, "Route not found.")
      if (parts.length === 2) {
        if (method === "GET") { send(response, 200, await store.listDatasets()); return }
        if (method === "POST") {
          const input = await json(request)
          send(response, 201, await store.createDataset((input as { name?: unknown } | null)?.name)); return
        }
      }
      const datasetId = parts[2] || ""
      if (parts.length === 3 && method === "GET") { send(response, 200, await store.getDataset(datasetId)); return }
      if (parts.length === 4 && parts[3] === "recordings") {
        if (method === "GET") { send(response, 200, await store.listRecordings(datasetId)); return }
        if (method === "POST") {
          send(response, 201, await addReference(store, datasetId, await body(request), url.searchParams.get("label"), url.searchParams.get("note") || "")); return
        }
      }
      if (parts.length === 6 && parts[3] === "recordings") {
        if (method === "GET" && parts[5] === "audio") {
          const audio = await store.audio(datasetId, parts[4])
          response.writeHead(200, { "Content-Type": "audio/wav", "Content-Length": audio.length, "Cache-Control": "no-store" })
          response.end(audio); return
        }
        if (method === "PUT" && parts[5] === "segments") {
          send(response, 200, await store.replaceSegments(datasetId, parts[4], await json(request))); return
        }
      }
      if (parts.length === 4 && parts[3] === "detect" && method === "POST") {
        send(response, 200, await detect(store, datasetId, await body(request), settings(url.searchParams))); return
      }
      if (parts.length === 4 && parts[3] === "events" && method === "GET") {
        send(response, 200, await store.listEvents(datasetId)); return
      }
      throw new ApiError(404, "Route not found.")
    } catch (error) {
      const status = error instanceof ApiError ? error.status : 500
      if (status === 500) console.error(error)
      if (!response.headersSent) send(response, status, { error: status === 500 ? "Local storage or processing failed." : (error as Error).message })
      else response.end()
    }
  })
}

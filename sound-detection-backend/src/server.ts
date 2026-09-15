import path from "node:path"
import { createApp } from "./http"

const port = Number(process.env.PORT || "5190")
if (!Number.isInteger(port) || port < 1 || port > 65535) throw new Error("PORT must be between 1 and 65535.")
const dataDir = path.resolve(process.env.DATA_DIR || "data")
const allowedOrigins = process.env.ALLOWED_ORIGINS?.split(",").map(value => value.trim()).filter(Boolean)
const server = createApp({ dataDir, allowedOrigins })
server.listen(port, "127.0.0.1", () => {
  console.log(`Sound Detection Backend: http://127.0.0.1:${port}`)
  console.log(`Local files: ${dataDir}`)
  console.log("Database: none")
})
server.on("error", error => { console.error(error.message); process.exitCode = 1 })
for (const signal of ["SIGINT", "SIGTERM"] as const) process.on(signal, () => {
  server.closeAllConnections()
  server.close(() => process.exit(0))
})

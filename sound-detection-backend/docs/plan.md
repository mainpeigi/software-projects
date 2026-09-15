# Standalone backend scope

- Reuse the original TypeScript acoustic engine and its tests, without the Carousel interface or server.
- Store datasets, recordings, annotations and results as local WAV and JSON files.
- Expose an HTTP API for reference upload and detection, and provide a reproducible synthetic demo.
- Run independently with Node.js 24 and no database, cloud credentials or online runtime services.
- Preserve the original source snapshot and SHA-256 inventory in `source-manifest.json`.
- Publish the sources under `Info-scoala/sound-detection-backend`, with build artifacts and local data excluded from Git.
- Deliver the compiled build and synthetic example data separately in `sound-detection-github-transfer.zip`.

See `verification.md` for the checks performed on the prepared package.

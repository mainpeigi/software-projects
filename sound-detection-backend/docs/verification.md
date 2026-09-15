# Verification record

## 15 September 2026: GitHub and laptop package

- Prepared an independent copy for `Info-scoala/sound-detection-backend`, without changing the original project or its engine.
- Node.js `v24.19.0`; `npm ci` installed the locked development dependencies successfully (0 reported vulnerabilities).
- Fresh `npm test`: 91 tests passed across 11 files. Fresh `npm run build`: TypeScript checking and production bundling passed.
- All 23 original files match the SHA-256 inventory and source commit `a66549c00bff2875effd9b132c2be1116ef000c9`.
- Generated a new synthetic dataset for this package with `node dist/demo.js`; two events were detected near 1.2 and 2.8 seconds. Existing source-project recordings were not copied.
- Created and inspected `sound-detection-github-transfer.zip`: 55 files containing sources, documentation, the npm lockfile, compiled JavaScript and synthetic WAV/JSON data. No `node_modules`, `.git`, `.env` or npm credentials file is included.
- Extracted the archive into a separate folder with spaces in its path, then ran the compiled demo successfully. The extracted project has no `node_modules`; compiled imports reference only included chunks and Node.js built-ins.
- Ran the extracted compiled HTTP application on a temporary loopback port: health reported `status=ok`, `storage=files`, `database=false`; the packaged dataset, reference and saved events were restored; a WAV detection request returned two events.
- Scanned the 45 source/documentation/configuration candidates for credential patterns, URLs and internal IP addresses. No credential patterns or internal private addresses were found. The dynamic HTTP host expression and `example.org` CORS rejection fixture were reviewed and retained.

README and transfer instructions distinguish cloning the source subfolder from using the compiled ZIP. The engine, API implementation and tests are unchanged. Git excludes runtime data, build output, development dependencies and local configuration.

## 15 September 2026: Erasmus preparation

- `npm test`: 91 tests passed across 11 files, including 6 backend HTTP tests.
- `npm run build`: TypeScript checking and the Node.js bundle build passed.
- All 23 original source files still match the SHA-256 hashes in the inventory.
- Added `docs/ERASMUS-DEMO.md`: an English five-minute demonstration guide, architecture explanation, limitations and likely questions.

## 14 September 2026: standalone and transfer checks

- `npm run demo`: two synthetic sound events detected near 1.2 and 2.8 seconds.
- `npm start`: the HTTP server started at `http://127.0.0.1:5190`.
- `GET /health`: `status=ok`, `storage=files`, `database=false`.
- An actual HTTP detection request processed 372 frames and returned two saved events.
- The transfer ZIP was extracted into a separate folder containing spaces in its path. The compiled server started without `node_modules` or a database, and restored the dataset, WAV recording and saved results.

The engine is a consistent snapshot captured before concurrent changes to the original Carousel project. Those later changes are not included.

The current transfer ZIP includes source files, compiled JavaScript, the npm lockfile, documentation and newly generated synthetic demo data. Installed dependencies and Git history are excluded. With Node.js 24 installed, start it using `node dist/server.js`; run the offline demonstration using `node dist/demo.js`.

These checks verify the implementation and synthetic demonstration. They do not establish accuracy on real-world recordings or compliance with an Erasmus assignment whose requirements have not been supplied.

# Portable Acoustic Anomaly Detection

## Project overview

This project detects selected sound events in WAV recordings by comparing their acoustic features with labelled reference recordings. It runs as a local Node.js backend and stores its data in ordinary WAV and JSON files. It does not require access to the original application's database or a cloud service.

The acoustic engine was extracted from Carousel. This standalone version adds a Node.js HTTP API, file storage, a reproducible demonstration and backend integration tests. The original source files and their hashes are listed in `docs/source-manifest.json`.

## How detection works

1. Load reference recordings labelled `Anomaly`, optionally with `Normal` examples.
2. Mark the time intervals containing the sound of interest, or use automatic onset detection.
3. Analyse short audio frames using FFT and spectral bands.
4. Estimate background noise and compare patterns above that background with the references.
5. Apply similarity, margin and timing rules to combine matching frames into sound events.
6. Return the event times and reference matches as JSON, and save the result locally.

The matching pipeline uses 48 kHz audio, frames of 2048 samples and a hop of 512 samples. It performs reference-based signal processing; it does not train a neural network.

## Five-minute demonstration

Use the transfer ZIP, which includes the compiled backend. Install Node.js 24 in advance and open a terminal inside the extracted project folder.

**1. State the objective — 30 seconds**

"I am demonstrating a portable backend that detects sound events using reference recordings. It can run on another laptop without a database or an internet connection once Node.js is installed."

**2. Run the reproducible example — 1 minute**

```powershell
node dist/demo.js
```

The demonstration creates a reference recording and a separate four-second test recording with two synthetic snaps. The expected output is `detectedSounds: 2`, with events near 1.2 and 2.8 seconds. It prints the dataset identifier and saves `data/demo-input.wav`.

Explain that synthetic examples make the demonstration reproducible. These results do not establish accuracy on real-world recordings.

**3. Start the API — 1 minute**

```powershell
node dist/server.js
```

In a second PowerShell terminal:

```powershell
Invoke-RestMethod http://127.0.0.1:5190/health
Invoke-RestMethod http://127.0.0.1:5190/api/datasets
```

Show `storage: files` and `database: false`, then show that the demonstration dataset is available through the API. Stop the server with Ctrl+C when finished.

**4. Explain the implementation — 1 minute**

- `src/lib/`: feature extraction, background modelling, matching, event detection and calibration.
- `src/detection.ts`: connects WAV recordings to the engine.
- `src/storage.ts`: stores datasets, reference recordings and results in local files.
- `src/http.ts`: exposes the API for a future interface.
- `tests/backend.test.ts`: verifies the complete API workflow and persistence after restart.

**5. Explain limitations and next steps — 1 minute**

This version analyses complete WAV files. It has no graphical interface and does not open the microphone. Real-time capture and its transport to the backend are future integration work; the original frame-based detector remains available as a library.

The API expects 48 kHz WAV input, limited to 64 MiB and ten minutes per recording. Reference quality, microphone placement and background conditions can affect detection. Real recordings with independent labels are needed to measure precision, recall and false alarms for a chosen use case.

## Questions to be ready to answer

**Why use local files instead of a database?**

To remove dependence on the original database and simplify transport between laptops. The files preserve recordings, annotations and results across restarts.

**What prevents every loud sound from becoming an alarm?**

The engine evaluates similarity to the selected anomaly references, separation from normal references, level above background and event timing. Their effectiveness depends on the reference dataset.

**What was reused and what was added?**

The signal-processing engine and its original tests were reused from Carousel. The standalone Node.js backend, file-based integration, HTTP tests, demonstration and transfer package were added for independent use.

**What has been validated?**

The automated suite covers the engine and the backend. Backend checks include persistence after restart, simultaneous uploads, invalid inputs and detection of known synthetic events. The transfer package has also been run from a separate folder without `node_modules` or a database. See `docs/verification.md` for the recorded checks.

# SpecTrack

A standalone backend for comparing versions of a project's requirements, components and checklists. It identifies additions, removals, edited fields and order-only changes, then produces a structured JSON result and a readable HTML change report.

The core can be used by a future web or desktop interface. The current project provides an HTTP API and a command-line demonstration; it does not include a requirements editor.

## What it does

- Compares project descriptions, categories, component quantities and required/optional status.
- Tracks component movement between categories, linked-part details and specification changes.
- Compares checklist descriptions, deadlines, roles and severity.
- Distinguishes reordered rows from edited content.
- Keeps immutable version snapshots in local JSON files and exports an HTML report.
- Validates stable IDs, references and input sizes before comparing or saving.

No database, account, API key or external service is required. The demonstration uses invented school-project data. Production code has no NuGet package dependencies beyond the .NET framework and the local core project.

## Ready Windows package

Extract **all** of `spec-track-windows-portable.zip` into a writable folder on Windows x64.

- Run `DEMO.bat`, then open `storage/demo-report.html` in a browser.
- Run `START.bat` to start the API at **http://127.0.0.1:5192**.

The self-contained package includes .NET. Close the program before copying its complete folder, including `storage/`, to another laptop. [Romanian transfer instructions](TRANSFER.md).

## Run from source

Install the .NET 10 SDK, then run from this project's folder:

```powershell
dotnet run --project src/SpecTrack.Api/SpecTrack.Api.csproj --configuration Release --no-launch-profile -- --demo
dotnet run --project src/SpecTrack.Api/SpecTrack.Api.csproj --configuration Release --no-launch-profile
```

The demo prints the output paths. Source runs normally store data in `src/SpecTrack.Api/storage/`. The first restore/build requires Internet; subsequent execution of the built program works offline.

The sample compares two versions of a classroom weather-station plan. Expected summary: **2 added rows, 1 removed row, 3 changed rows**, plus a description change and a project-option change. The temperature sensor changes quantity and specifications; a display is replaced by local data storage; verification and documentation tasks change.

## API

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/health` | Check file storage mode and no-DB status |
| GET | `/api/snapshots?limit=100` | List recent saved snapshots; limit 1–500 |
| POST | `/api/snapshots` | Save `{ "label": "v1", "snapshot": ... }` |
| GET | `/api/snapshots/{id}` | Read a saved snapshot |
| POST | `/api/compare` | Compare `{ "fromId": "...", "toId": "..." }` |
| POST | `/api/compare/preview` | Compare `{ "from": ..., "to": ... }` without saving |
| GET | `/api/compare/{fromId}/{toId}/report` | Download an HTML change report |

Use the JSON structures in `examples/`. Keep a row's `key` unchanged between versions to identify the same item. New rows get new GUIDs. Snapshot document IDs are generated when saving; they are separate from row keys.

Requests use `application/json` and are limited to 4 MiB. Invalid input returns a problem response; missing snapshots return 404. Null row lists are accepted as empty and are normalized on save. Snapshots cannot be overwritten through the API.

`summary.changed` counts changed rows across categories, components and checklist items; `reordered` is the subset whose only change is order. Top-level description/name changes and project options have separate counters/flags. Specification names are compared without case sensitivity; most other text comparisons preserve the original engine's case-sensitive semantics.

For API examples and future UI integration, see [docs/API.md](docs/API.md).

## Configuration and verification

`Storage:Directory` chooses the storage folder; relative paths use the application's content root. `Cors:Origins` can opt in specific local frontend origins, such as `http://localhost:5173`. The API binds to loopback and does not offer remote/multi-user access.

```powershell
dotnet test SpecTrack.slnx --configuration Release
.\PUBLISH-WINDOWS.bat
```

## Origin and contribution

The comparison algorithm and generic snapshot models were adapted from the requirement-template comparison module in ReqWire2. This extraction separates that feature from its original infrastructure and adds local persistence, validation, a new API, reports and tests. It does not claim the original algorithm was newly authored for this standalone project. [Source provenance](docs/PROVENANCE.md).

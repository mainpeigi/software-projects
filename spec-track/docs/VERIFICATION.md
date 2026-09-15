# Verification — 2026-09-15

Command: `dotnet test SpecTrack.slnx --configuration Release`.

- Core: 55 passed, 0 failed.
- API: 59 passed, 0 failed.
- Release builds completed without warnings or errors.

Coverage includes added/removed/edited/reordered items, category moves, flags, part metadata, case-insensitive specifications, schema/reference/key validation, immutable saved versions, 24 concurrent creates, storage reopening and relocation, JSON and HTML output, escaped user text, invalid requests, 4 MiB body limits and allowed/forbidden browser origins.

The API tests first failed against a host without the feature, then passed after implementation. Nullable row lists intentionally preserve source behavior: they are accepted as empty and normalized before storage.

The real command-line demo produced the expected result: 2 added rows, 1 removed, 3 changed, 0 reorder-only, 1 project-field change and changed project options. It created two immutable JSON snapshots plus `demo-result.json` and a standalone `demo-report.html`.

The original repository was read only. Its clean working tree and the selected source hashes are checked again before delivery. No original database was accessed.

The Windows x64 self-contained package was copied into a separate folder containing spaces. Running `SpecTrack.Api.exe --demo` created the expected report and two snapshots without an installed-SDK command. Restarting that copy as a server bound it to `127.0.0.1:5192`; real HTTP checks restored both snapshots, recomputed the expected comparison and downloaded the HTML report successfully. Launchers set their working directory to the application folder so configuration and storage move together.

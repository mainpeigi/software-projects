# Verification — 2026-09-15

`dotnet test tests/Datemulte.Portable.Tests/Datemulte.Portable.Tests.csproj --configuration Release`:

- 11 passed, 0 failed.
- Local SQLite startup with no cloud or database connection configuration.
- HTTP pages and bundled ECharts asset.
- Real CSV and Excel uploads and statistics.
- Stable local profile and persisted preferences.
- Saved sessions and cell edits surviving process-host restart.
- Copied data directory loading successfully from a new path; deleting the copied session leaves the original copy intact.

The original project compiled before adaptation. Known original warnings remain, including the legacy NCalc package compatibility warning and unused cloud UI/nullability warnings. No PostgreSQL server or production data was used in verification.

The tests cover services and HTTP responses. They do not simulate mouse interaction with the full Blazor interface.

The Windows x64 self-contained publish also succeeded. A copy in a different folder containing spaces started directly through `Datemulte_2.exe`, created its own `local-data/datemulte.db`, and returned HTTP 200 for health, home, plotter, profile, the sample CSV, ECharts, Blazor and MudBlazor JavaScript. This check used port 5198 to avoid interference with the normal 5188 port. No SDK command or external database was used to start that copy.

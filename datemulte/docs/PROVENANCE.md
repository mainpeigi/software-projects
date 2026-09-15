# Source and portable adaptation

Original project: `csharp-projects/stundeti/datemulte` (GitLab).
Source snapshot: `ffae00b40ae042a0f92df4f2c8382fc980355eb0`, dated 2026-07-02.
Observed history: 9 commits, dated 2025-11-12 and 2026-07-02, under Husaru Serban-Adrian and Husaru.

The source snapshot includes the Blazor interface, parsing, statistics, formula evaluation, data sessions and persistence model. This copy preserves those origins; it does not claim that every original component was newly written for this portable edition.

Portable adaptation (2026-09-15):

- Separate local host, bound to 127.0.0.1, retaining antiforgery and security middleware.
- Embedded SQLite storage, schema creation and stable local file ownership.
- Relative upload paths, so saved sessions survive a folder move.
- Local profile/home/login presentation; cloud routes, shared-session endpoints and jobs are not hosted.
- Bundled ECharts library/themes and synthetic sample data.
- Integration tests, Windows launcher, self-contained publish script and transfer documentation.

Original production configuration, private Git history, environment files, certificates, cloud migrations and deployment scripts are excluded. Third-party package and asset licenses remain with their original authors.

`source-manifest.json` records hashes of the original source files selected before adaptation; hashes describe the source snapshot, not necessarily the current edited files.

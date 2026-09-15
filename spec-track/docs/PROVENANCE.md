# Source provenance

Source project: ReqWire2.
Source revision: `fde5e49fb6ced4dc3590096803c866195454c185`, dated 2026-08-27.

Selected original files:

- `backend/src/ReqWire2.Application/Services/TemplateDiffCalculator.cs`
- `backend/src/ReqWire2.Application/DTOs/TemplateSnapshot.cs`
- The generic comparison-result model subset of `backend/src/ReqWire2.Application/DTOs/TemplateDiffDto.cs`

The standalone core preserves the original stable-key comparison approach, whitespace normalization, category/component/checklist changes, case-insensitive specification comparison and reorder-only detection. Namespaces are independent. Infrastructure-dependent wrappers, database services, account systems, external integrations, configuration and real project data were not copied.

The standalone adaptation adds validation instead of silently ignoring duplicate IDs, detects changed metadata for the same linked part, provides immutable local JSON storage, a loopback HTTP API, an escaped HTML report, a synthetic school-project example and automated tests.

This provenance identifies reused code rather than presenting the whole feature as newly written from scratch. Original repositories and files remain unchanged. There is no company branding or real company data in the runtime or examples.

`source-manifest.json` records the original selected file hashes before adaptation. These are working-file byte hashes and can differ from Git blob hashes because of line-ending normalization.

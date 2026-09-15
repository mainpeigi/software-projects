# API examples

Start the server, then run these PowerShell commands from the project folder. They use only the synthetic examples included in this project.

```powershell
$baseUrl = 'http://127.0.0.1:5192'
$first = Get-Content -LiteralPath 'examples/school-project-v1.json' -Raw | ConvertFrom-Json
$second = Get-Content -LiteralPath 'examples/school-project-v2.json' -Raw | ConvertFrom-Json
$body1 = @{ label = 'v1'; snapshot = $first } | ConvertTo-Json -Depth 30
$body2 = @{ label = 'v2'; snapshot = $second } | ConvertTo-Json -Depth 30
$saved1 = Invoke-RestMethod -Uri "$baseUrl/api/snapshots" -Method Post -ContentType 'application/json' -Body $body1
$saved2 = Invoke-RestMethod -Uri "$baseUrl/api/snapshots" -Method Post -ContentType 'application/json' -Body $body2
$request = @{ fromId = $saved1.id; toId = $saved2.id } | ConvertTo-Json
$result = Invoke-RestMethod -Uri "$baseUrl/api/compare" -Method Post -ContentType 'application/json' -Body $request
$result.summary
Invoke-WebRequest -Uri "$baseUrl/api/compare/$($saved1.id)/$($saved2.id)/report" -OutFile 'change-report.html'
```

Each saved snapshot has a generated `id`, a `label`, UTC `createdAt` and the complete `snapshot`. List responses return metadata. Comparison responses contain `from`/`to` metadata, `diff`, and `summary`. Preview responses contain only `diff` and `summary` and create no snapshot files.

The input schema is version 1. Categories, components and checklist items each have a nonempty stable GUID `key`, unique within that section. A component's `categoryKey` must reference a category in the same snapshot; `null` means uncategorized. Quantity is between 1 and 1,000,000. Blank names, unsupported schema versions, null row entries and duplicate IDs are rejected.

Limits: 5,000 aggregate rows per snapshot; 200 characters for short text; 4,000 for descriptions; 256 project flags; 256 specifications per part and 20,000 total specifications. List requests accept `limit` from 1 to 500 (default 100). Snapshots are immutable through the API: saving a new version creates a new document ID.

Keep row keys stable across versions. Changing a row's key produces a removal and an addition, rather than an edit. Specification names ignore case; blank specification names are ignored during comparison. Null category/component/checklist arrays are treated as empty. `dueDateOffsetDays` is a relative integer offset and may be negative for work scheduled before a milestone.

## Connecting a future interface

The API defaults to no cross-origin browser access. For a local frontend, set the following in `src/SpecTrack.Api/appsettings.json` (or the corresponding file in the Windows package):

```json
"Cors": { "Origins": ["http://localhost:5173"] }
```

Restart the API. Allowed origins must be HTTP(S) loopback origins with no path or credentials. Command-line clients do not need an Origin header. Unapproved browser origins cannot submit POST requests.

The HTML report contains escaped text and no scripts or remote assets. It can be opened or printed separately after download.

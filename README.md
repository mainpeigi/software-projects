# Info-scoala

Three independent projects prepared for a school/Erasmus portfolio. Each folder contains its own source, setup instructions, verification and provenance notes.

| Project | What it does | Run from source |
| --- | --- | --- |
| [Datemulte](datemulte/) | Imports CSV/Excel, plots interactive charts, calculates statistics and saves local sessions. | .NET 9 SDK or a compatible newer SDK; no database server. |
| [Sound detection backend](sound-detection-backend/) | Learns reference sounds and detects matching events in WAV recordings through a local HTTP API. | Node.js 24; no database. |
| [SpecTrack](spec-track/) | Compares requirement/checklist versions, keeps local snapshots and exports change reports. | .NET 10 SDK; no database. |

## Datemulte

```powershell
cd datemulte
dotnet run --project Datemulte_2.csproj --configuration Release --no-launch-profile
```

Open **http://127.0.0.1:5188**. Start with `samples/temperatures.csv`. [Romanian transfer guide](datemulte/TRANSFER.md).

## Sound detection backend

```powershell
cd sound-detection-backend
npm ci
npm test
npm run demo
npm start
```

The API listens on **http://127.0.0.1:5190**. [API documentation](sound-detection-backend/README.md) · [Romanian transfer guide](sound-detection-backend/TRANSFER.md).

## SpecTrack

```powershell
cd spec-track
dotnet run --project src/SpecTrack.Api/SpecTrack.Api.csproj --configuration Release --no-launch-profile -- --demo
```

The demo prints the HTML report path. Run again without `-- --demo` to start the API at **http://127.0.0.1:5192**. [Romanian transfer guide](spec-track/TRANSFER.md).

## Project presentation

All projects include synthetic demonstration data or generators and preserve their source origins. Sound detection and SpecTrack are backends; SpecTrack also exports a readable HTML report, while Datemulte includes a Blazor interface. Describe the parts you personally implemented and the libraries or source code reused.

No original database credentials, production environment files, private Git history, build dependencies or personal runtime data are included. Compiled transfer archives are delivered separately; source instructions explain how to rebuild them.

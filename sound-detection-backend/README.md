# Sound Detection Backend

Portable Node.js backend for acoustic anomaly detection from WAV recordings, with local file storage and a reproducible offline demonstration. The signal-processing engine and its original tests come from Carousel; the HTTP API, file storage and transfer packaging support independent use.

Backend local pentru detecția sunetelor, extras din motorul `acoustic-*` al aplicației Carousel. Rulează cu **Node.js 24**, fără interfață și fără bază de date: înregistrările sunt fișiere WAV, iar metadatele și rezultatele sunt fișiere JSON în `data/`.

Algoritmul original era în TypeScript, în partea web a aplicației. Aici rulează pe server. Codul C# pentru PostgreSQL și autentificarea Carousel nu sunt necesare acestui backend.

Pentru mutarea pe alt laptop și rularea buildului deja compilat, vezi [TRANSFER.md](TRANSFER.md).

Pentru demonstrația Erasmus, vezi [ghidul de prezentare în engleză](docs/ERASMUS-DEMO.md).

## Pornire

```powershell
git clone https://github.com/mainpeigi/Info-scoala.git
cd Info-scoala\sound-detection-backend
npm ci
npm start
```

Dacă ai deja repository-ul, deschide terminalul direct în subdirectorul `sound-detection-backend` și începe cu `npm ci`. Acest proiect are propriile dependențe și pornește independent de celelalte proiecte din repository.

API-ul pornește la **http://127.0.0.1:5190**. Verificare:

```powershell
Invoke-RestMethod http://127.0.0.1:5190/health
```

Răspunsul include `status: ok`, `storage: files` și `database: false`. Nu ai nevoie de `.env`, credențiale sau acces la DB-ul aplicației originale. După instalare și build, serverul funcționează fără internet.

## Exemplu complet, fără microfon sau fișiere proprii

```powershell
npm run demo
```

Exemplul generează o referință și un WAV cu două sunete, rulează detecția și afișează cele două evenimente. Salvează setul, referința și rezultatul în `data/`, inclusiv `data/demo-input.wav`. La fiecare rulare creează un set demonstrativ nou.

## API pentru viitoarea interfață

| Metodă și rută | Date / rezultat |
| --- | --- |
| `GET /health` | Starea backendului |
| `GET /api/datasets` | Seturile locale |
| `POST /api/datasets` | JSON `{ "name": "Sunetele mele" }`; returnează setul și `id` |
| `GET /api/datasets/{id}` | Metadatele unui set |
| `GET /api/datasets/{id}/recordings` | Referințele audio și segmentele lor |
| `POST /api/datasets/{id}/recordings?label=Anomaly` | WAV în corpul cererii; eticheta poate fi `Anomaly` sau `Normal`, cu `note` opțional |
| `GET /api/datasets/{id}/recordings/{recordingId}/audio` | WAV-ul înregistrării |
| `PUT /api/datasets/{id}/recordings/{recordingId}/segments` | Array JSON cu `{ "startMs": 795, "endMs": 835, "kind": "Auto" }` |
| `POST /api/datasets/{id}/detect` | WAV în corpul cererii; returnează detecțiile și salvează rezultatul |
| `GET /api/datasets/{id}/events` | Rezultatele detecțiilor salvate pentru set |

Încărcările audio folosesc corp binar, de exemplu `Content-Type: audio/wav`, nu `multipart/form-data`. Fișierele trebuie să fie WAV PCM 16/24/32-bit sau float 32-bit, la **48.000 Hz**. Canalele sunt combinate în mono. Limite: 64 MiB și 10 minute; minimum 2048 de mostre. Pentru alt format poți converti înainte de upload:

```powershell
ffmpeg -i sunet.mp3 -ar 48000 -ac 1 sunet.wav
```

FFmpeg este opțional și nu este necesar pentru pornire sau pentru demo.

Exemplu cu propriile înregistrări:

```powershell
$api = 'http://127.0.0.1:5190'
$dataset = Invoke-RestMethod "$api/api/datasets" -Method Post -ContentType 'application/json' -Body '{"name":"Sunetele mele"}'
$datasetId = $dataset.id
Invoke-RestMethod "$api/api/datasets/$datasetId/recordings?label=Anomaly" -Method Post -ContentType 'audio/wav' -InFile '.\referinta.wav'
Invoke-RestMethod "$api/api/datasets/$datasetId/detect" -Method Post -ContentType 'audio/wav' -InFile '.\de-analizat.wav'
```

Adaugă cel puțin o referință `Anomaly`. Poți adăuga și referințe `Normal` pentru a reduce alarmele false. Adnotările delimitează sunetele căutate în milisecunde; fără adnotări, motorul caută automat momentele care se disting de fundal. Tipurile segmentelor sunt `Auto`, `Impulsive` și `Sustained`.

Parametrii opționali ai rutei `detect`: `threshold`, `minMargin`, `minLevelDb`, `adaptBackground=true|false`, `unknownBelow`. Dacă îi omiți, sunt folosite valorile motorului original. Rezultatul conține `alarms` cu `startMs`, `endMs` și `match`, plus calibrarea referințelor.

## Structură și personalizare

- `src/lib/`: algoritmii originali pentru FFT, fundal, potrivire, detecție, calibrare și evaluare; păstrați identic cu sursa.
- `src/storage.ts`: persistență în fișiere locale, inclusiv după repornire.
- `src/detection.ts`: conectează fișierele WAV la motor.
- `src/http.ts`, `src/server.ts`: API și pornire.
- `src/index.ts`: interfață de bibliotecă pentru integrare directă din alt cod Node.js.
- `examples/demo.ts`: exemplul executabil.
- `docs/source-manifest.json`: proveniența și hashurile fișierelor originale copiate.

API-ul analizează înregistrări WAV complete. Pentru o viitoare captură live, clasa exportată `AcousticDetector` poate procesa cadre succesive de 2048 de mostre, cu pasul de 512, prin `processSamples`; transportul fluxului audio din noua interfață se adaugă separat. Backendul nu deschide singur microfonul.

Serverul ascultă pe loopback și este destinat rulării locale. Interfețele de pe `http://localhost:5173` și `http://127.0.0.1:5173` sunt acceptate implicit. Poți configura adresa interfeței, portul și directorul de date:

```powershell
$env:PORT = '5190'
$env:DATA_DIR = 'D:\Proiecte\sunete-date'
$env:ALLOWED_ORIGINS = 'http://localhost:3000'
npm start
```

Datele sunt separate pe seturi și înregistrări; un upload nou nu rescrie un index comun. Procesarea WAV este sincronă în procesul Node, potrivită unei aplicații locale; pentru mai mulți utilizatori simultani, mută calculele într-un worker și adaugă autentificarea noului produs.

## Verificare și proveniență

```powershell
npm test
npm run build
```

Sunt incluse testele originale ale motorului și teste noi pentru API, persistență după repornire, uploaduri simultane, validare și detecție efectivă.

Proiectul este pregătit ca subdirector în `mainpeigi/Info-scoala`; comenzile Git se execută din repository-ul clonat. Nu inițializa un repository Git separat în acest subdirector. `data/`, `node_modules/`, `dist/` și configurațiile locale sunt excluse din Git. Arhiva de transfer include separat buildul și doar date demonstrative sintetice.

Motorul de procesare și testele sale sunt reutilizate din Carousel, din snapshotul `a66549c00bff2875effd9b132c2be1116ef000c9`. Cele 23 de fișiere originale și hashurile SHA-256 sunt în [docs/source-manifest.json](docs/source-manifest.json). Aceste fișiere au fost păstrate identic. Backendul Node.js, stocarea locală, API-ul HTTP, testele de integrare și exemplul executabil formează adaptarea pentru rulare independentă. Nu se pretinde că algoritmul original a fost creat pentru această prezentare și nu a fost atribuită o licență nouă codului extras.

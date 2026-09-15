# Transfer pe alt laptop

Arhiva `sound-detection-github-transfer.zip` conține directorul `sound-detection-backend/` cu sursele, backendul compilat în `dist/` și un set demonstrativ generat în `data/`. Datele incluse sunt sintetice; arhiva nu include înregistrări personale, `node_modules`, configurații locale sau istoricul Git.

## Rulare directă din ZIP

1. Instalează Node.js 24 pe laptopul nou. Verifică în terminal cu `node --version`.
2. Dezarhivează proiectul într-un director la alegere.
3. Deschide terminalul în directorul `sound-detection-backend` extras și rulează:

```powershell
node dist/server.js
```

Backendul pornește la `http://127.0.0.1:5190`. Nu trebuie să rulezi `npm install` pentru această variantă: buildul inclus folosește doar Node.js și fișierele din arhivă. După instalarea Node.js, rularea nu necesită internet sau DB. Oprești serverul cu Ctrl+C.

Într-un al doilea terminal poți verifica pornirea:

```powershell
Invoke-RestMethod http://127.0.0.1:5190/health
Invoke-RestMethod http://127.0.0.1:5190/api/datasets
```

Pentru demonstrația completă, din directorul proiectului:

```powershell
node dist/demo.js
```

Exemplul creează un set demonstrativ nou și raportează `detectedSounds: 2`. Vezi [ghidul de prezentare în engleză](docs/ERASMUS-DEMO.md).

Datele sunt citite și salvate în `data/`, relativ la directorul din care pornești comanda. Poți muta proiectul în orice cale; deschide terminalul în directorul lui înainte de pornire.

## Rulare și modificare din GitHub

Repository-ul conține sursele și lockfile-ul. Buildul și datele sunt excluse din Git. Cu acces la internet la prima instalare:

```powershell
git clone https://github.com/mainpeigi/Info-scoala.git
cd Info-scoala\sound-detection-backend
npm ci
npm start
```

Dacă ai clonat deja repository-ul, începe din subdirectorul `sound-detection-backend` cu `npm ci`. Acest proiect rulează independent de celelalte directoare. Nu rula `git init` în subdirector.

`npm ci` instalează versiunile din `package-lock.json`, iar `npm start` reconstruiește backendul și îl pornește. Pentru verificare: `npm test`. Pentru generarea datelor demonstrative din surse: `npm run demo`. După build poți porni offline cu `node dist/server.js`.

Pentru a păstra și înregistrările dintr-un ZIP atunci când folosești sursele clonate, copiază `data/` din arhivă în subdirectorul `sound-detection-backend` înainte de pornire.

## Transferul propriilor înregistrări

Pentru înregistrări adăugate ulterior, oprește backendul înainte de copiere și transferă întregul director `data/`. Păstrează împreună fișierele WAV și JSON și structura directoarelor.

Dacă folosești variabila `DATA_DIR` cu un director extern, copiază separat acel director și configurează pe laptopul nou calea corespunzătoare. Fișierele audio nu sunt salvate în Git, deoarece `data/` este ignorat intenționat.

## Proveniență

Motorul de procesare și testele sale provin din Carousel; snapshotul și hashurile celor 23 de fișiere sunt păstrate în [docs/source-manifest.json](docs/source-manifest.json). API-ul Node.js, persistența locală, testele de integrare și demo-ul permit rularea independentă. Publicarea acestui snapshot nu îi atribuie o licență nouă.

# SpecTrack pe alt laptop

## Pornire rapidă pe Windows 64-bit

1. Copiază `spec-track-windows-portable.zip`.
2. Dezarhivează **tot** conținutul într-un folder în care poți salva fișiere.
3. Rulează `DEMO.bat`.
4. Deschide `storage/demo-report.html` în browser.

Demonstrația compară două versiuni fictive ale unui proiect școlar și arată modificările. Fiecare rulare creează două versiuni noi; versiunile salvate anterior rămân păstrate.

Pentru server, rulează `START.bat` și deschide **http://127.0.0.1:5192/health**. Fereastra trebuie să rămână deschisă. Oprește serverul cu Ctrl+C. Dacă portul este ocupat, închide instanța SpecTrack pornită deja.

Pachetul include .NET. Nu instalezi PostgreSQL, Docker, Node.js sau altă bază de date și nu ai nevoie de conturi ori chei de acces.

## Datele proiectului

Versiunile sunt fișiere JSON în `storage/snapshots/`. Raportul demonstrației este în `storage/demo-report.html`; rezultatul complet este în `storage/demo-result.json`.

Închide programul și copiază întregul folder, inclusiv `storage/`, pentru a continua pe alt laptop. Codul proiectelor originale nu este necesar pentru rulare.

## Pentru modificarea codului sau a interfeței

Sursele sunt în arhiva `spec-track-source.zip`. Ai nevoie de SDK .NET 10. Rulează `START-SOURCE.bat` sau comenzile din README.

Logica de comparație este în `src/SpecTrack.Core`; API-ul, salvarea și raportul sunt în `src/SpecTrack.Api`. Poți conecta ulterior o interfață la API fără să schimbi algoritmul. Această versiune nu include un editor grafic de cerințe.

## Prezentare pentru dosar

Poți prezenta proiectul ca „SpecTrack — backend pentru compararea și urmărirea versiunilor cerințelor”. Arată raportul și explică identificatorii stabili, detectarea diferențelor și salvarea locală. Precizează ce logică ai preluat și ce ai adaptat sau implementat.

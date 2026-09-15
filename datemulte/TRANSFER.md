# Datemulte pe alt laptop

## Varianta simplă: Windows 64-bit

1. Copiază `datemulte-windows-portable.zip` pe celălalt laptop.
2. Dezarhivează **tot** conținutul într-un folder în care poți salva fișiere. Nu porni aplicația direct din ZIP.
3. Deschide `START.bat`.
4. Deschide în browser **http://127.0.0.1:5188**.

Nu trebuie să instalezi .NET, PostgreSQL, Docker sau Node pentru Datemulte. Nu sunt necesare conturi, parole sau acces la baza de date originală. Păstrează fereastra de pornire deschisă; oprește aplicația cu Ctrl+C.

## Demonstrație pentru Erasmus

- Intră în Data Plotter și încarcă `samples/temperatures.csv`.
- Apasă Load File și selectează Temperature. Poți pune Humidity pe a doua axă.
- Arată graficul, statisticile și o modificare a configurației graficului.
- Completează Session Name și apasă Save Session.
- Repornește aplicația și arată că sesiunea se deschide prin Load Session.

Datele din exemplu sunt sintetice. Media temperaturii este 23,5.

## Păstrarea datelor când muți aplicația

Salvează sesiunile, închide aplicația și copiază întregul folder, inclusiv `local-data/`. Acolo sunt fișierul `datemulte.db` și fișierele încărcate din `local-data/files/`. Nu copia doar executabilul și nu muta fișierele în timp ce aplicația rulează.

Modificările nesalvate și unele preferințe păstrate exclusiv în browser nu sunt transportate. Folosește Save Session înainte de mutare.

## Varianta din GitHub

Sursele sunt în folderul `datemulte` din repository-ul `mainpeigi/Info-scoala`. Pentru surse ai nevoie de SDK .NET 9 sau unul mai nou compatibil și de Internet la prima compilare. Rulează `START-SOURCE.bat` din folderul proiectului. `PUBLISH-WINDOWS.ps1` produce un nou pachet Windows care include runtime-ul.

Pachetul pregătit este pentru Windows x64. Pentru macOS/Linux folosește sursele cu SDK compatibil sau publică separat pentru platforma respectivă.

## Dacă portul este ocupat

Închide instanța Datemulte pornită deja. Alternativ, schimbă `Portable:Port` în `appsettings.json` (de exemplu 5189) și deschide aceeași valoare în browser. Aplicația este disponibilă doar pe laptopul pe care rulează.

Conturile online, partajarea cu alți utilizatori, webhook-urile și rapoartele programate nu sunt disponibile în această ediție locală.

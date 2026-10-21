# Zeitwerk – Spezifikation

Begriffe siehe [CONTEXT.md](CONTEXT.md).

## Stack & Betrieb

- .NET 10, Blazor Web App (Interactive Server), MudBlazor mit festem Dark-Theme (kein Umschalter)
- EF Core + PostgreSQL (Npgsql); Migrationen werden beim App-Start automatisch angewendet
- Docker Compose: `app`, `db` (benanntes Volume), `backup` (täglicher `pg_dump` nach `./backups`, die letzten 14 bleiben)
- Port `127.0.0.1:8080`, konfigurierbar über `.env`
- Kein Login, keine Benutzerverwaltung
- Speicherung in UTC (`timestamptz`), Anzeige in `Europe/Berlin` (konfigurierbar über `Zeitwerk__TimeZone`)
- UI-Sprache Deutsch

## Datenmodell

- **Kunde**: Name, archiviert
- **Projekt**: Kunde, Name, Farbe (beim Anlegen vorgeschlagen, frei wählbar), archiviert
- **Zeiteintrag**: Projekt, Tätigkeit (Freitext, optional), Beginn, Ende (`null` = offen)
- Es gibt höchstens einen offenen Zeiteintrag.
- Kunden und Projekte mit Zeiteinträgen können nur archiviert werden; hart löschen nur ohne Zeiteinträge.
  Archivierte Stammdaten verschwinden aus der Auswahl, bleiben in Historie und Export sichtbar.

## Timer-Regeln

Raster = 5 Minuten. Toleranz = 60 s (konfigurierbar über `Zeitwerk__ToleranceSeconds`, 0 = streng).

- `abrunden(t)` = letzte Rastergrenze ≤ t
- `aufrunden(t)` = `abrunden(t)`, falls `t - abrunden(t) ≤ Toleranz`, sonst nächste Rastergrenze

| # | Situation | Ergebnis |
|---|-----------|----------|
| R1 | **Start** aus dem Leerlauf um 09:03 | Beginn 09:00 (abgerundet) |
| R2 | Start, aber letzter Zeiteintrag endet nach `abrunden(jetzt)` (Stopp 12:02 → Ende 12:05, Start 12:03) | Beginn = Ende des letzten Eintrags (12:05), on hold. Gebuchte Zeit wird nie gekürzt. |
| R3 | Start mit gleichem Projekt + gleicher Tätigkeit, der direkt an den letzten Eintrag anschließt | **Fortsetzen**: letzter Eintrag wird wieder geöffnet |
| R4 | **Wechsel** um 09:47 (A läuft seit 09:00) | A endet 09:50, B beginnt 09:50 (on hold bis dahin) |
| R5 | Wechsel um 09:50:20 (innerhalb Toleranz) | A endet 09:50, B beginnt 09:50 |
| R6 | Wechsel, während der offene Eintrag on hold ist | Der on-hold-Eintrag bekommt das neue Projekt/die neue Tätigkeit (keine Zeit verbraucht) |
| R7 | Wechsel zurück auf den Eintrag direkt davor, während on hold (A→B um 09:47, zurück zu A um 09:48) | B entfällt, A wird wieder geöffnet |
| R8 | Wechsel, der aufgerundet auf den Beginn des laufenden Eintrags fällt (Beginn 09:50, Wechsel 09:50:30) | wie R6 – kein Eintrag mit Länge 0 |
| R9 | Wechsel auf gleiches Projekt + gleiche Tätigkeit | keine Änderung |
| R10 | **Stopp** um 12:02 | Ende 12:05 |
| R11 | Stopp, während on hold | on-hold-Eintrag wird verworfen; der vorherige behält sein Ende |
| R12 | Stopp, der aufgerundet auf den Beginn fällt | Eintrag wird verworfen (Länge 0) |
| R13 | Tätigkeit des offenen Eintrags bearbeiten | wird direkt geändert, kein Wechsel |
| R14 | Ein Zeiteintrag wird über Mitternacht (lokale Zeit) beendet | wird an jeder Mitternacht aufgeteilt |
| R15 | Offener Eintrag läuft länger als 10 h | Hinweis in der UI, kein Auto-Stopp |

Eine Pause ist ein Stopp; die Lücke zwischen zwei Einträgen ist die Pause.

## Manuelle Einträge

- Anlegen, bearbeiten, löschen; Beginn/Ende im 5-Minuten-Raster
- Beginn < Ende; keine Überlappung mit anderen Einträgen (ein offener Eintrag belegt ab seinem Beginn alles)
- Einträge über Mitternacht werden beim Speichern aufgeteilt (R14)

## Oberfläche

- **Timer-Leiste** fest oben auf jeder Seite: Projekt, Tätigkeit (editierbar, R13), laufende Dauer, on-hold-Hinweis
  („startet um 09:50“), Wechseln/Start und Stopp, Schnellwahl der zuletzt genutzten Projekt/Tätigkeit-Kombinationen,
  Warnung nach 10 h (R15)
- **Tagesansicht** (Startseite = heute): horizontale Zeitleiste in Projektfarben (Auto-Zoom, Standard 07–19 Uhr,
  erweitert sich bei Einträgen außerhalb), Jetzt-Linie, laufend/on hold gekennzeichnet, Tooltip, Klick öffnet
  Bearbeiten-Dialog; darunter Eintragsliste mit Tagessumme; Vor/Zurück-Navigation
- **Kalender**: Monatsansicht mit Tagessummen, Klick öffnet den Tag
- **Stammdaten**: Kunden und Projekte verwalten, archivieren
- **Export**: siehe unten
- Tätigkeit mit Autovervollständigung aus früheren Eingaben desselben Projekts

## CSV-Export

- Einzelbuchungen, Filter: Zeitraum (Pflicht), Kunde/Projekt (optional); nur abgeschlossene Einträge
- `;` als Trenner, UTF-8 **ohne** BOM, Dezimalkomma
- Spalten: `Datum;Start;Ende;Dauer;Stunden;Kunde;Projekt;Tätigkeit` – Dauer als `hh:mm`, Stunden als `1,75`

## Bewusst später

Aggregierter Export, Drag & Drop in der Zeitleiste, Abrechenbar-Flag.

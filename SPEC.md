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

- **Kunde**: Name, archiviert, Ticket-URL und Ticket-Muster (optional, siehe [Ticket-Links](#ticket-links))
- **Projekt**: Kunde, Name, Farbe (beim Anlegen vorgeschlagen, frei wählbar), archiviert, Ticket-URL und
  Ticket-Muster (optional, überschreiben die Angaben des Kunden)
- **Zeiteintrag**: Projekt, Tätigkeit (Freitext, optional), Beginn, Ende (`null` = offen)
- Es gibt höchstens einen offenen Zeiteintrag.
- Kunden und Projekte mit Zeiteinträgen können nur archiviert werden; hart löschen nur ohne Zeiteinträge.
  Archivierte Stammdaten verschwinden aus der Auswahl, bleiben in Historie und Export sichtbar.
- **Soll-Arbeitszeit**: Gültig ab (eindeutig), Soll je Wochentag Mo–So
- **Abwesenheit**: Datum (eindeutig), Art (Urlaub, Krankheit, Feiertag); immer ein ganzer Tag
- **Kontostart**: Datum, Startsaldo (höchstens ein Datensatz)

## Überstundenkonto

| # | Regel |
|---|-------|
| K1 | Soll eines Tages = Wert seines Wochentags aus der Soll-Arbeitszeit mit dem spätesten Gültig-ab ≤ Tag. Vor der ersten gespeicherten Version gilt der Standard Mo–Fr je 8 h, Sa/So 0. Eine neue Version ändert also nie Tage vor ihrem Gültig-ab. |
| K2 | An einem Abwesenheitstag ist das Soll 0 (kein angerechnetes Soll). Trotzdem gebuchte Zeit zählt voll als Ist, also als Überstunden. |
| K3 | Ist eines Tages = gebuchte Zeit am lokalen Tag; ein laufender Eintrag zählt bis jetzt. |
| K4 | Ein Tag zählt zum Konto, wenn Kontostart ≤ Tag ≤ heute. Der laufende Tag zählt mit vollem Soll. Zukünftige Tage zählen nicht. |
| K5 | Saldo = Startsaldo + Σ (Ist − Soll) aller zählenden Tage. |
| K6 | Soll/Ist/Saldo einer Woche oder eines Monats umfassen nur die zählenden Tage dieses Zeitraums. |
| K7 | Ohne gespeicherten Kontostart beginnt das Konto am Tag des ersten Zeiteintrags (ohne Einträge: heute) mit Startsaldo 0. |

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
| R15 | Offener Eintrag läuft länger als 10 h oder über Mitternacht (lokale Zeit) hinaus | Hinweis „Vergessen zu stoppen?“ in der UI, kein Auto-Stopp |
| R16 | **Ende nachtragen** zum Hinweis aus R15 („Timer läuft seit gestern 17:30 – wann hast du aufgehört?“) | Nutzer wählt ein Ende: auf dem Raster, nach dem Beginn, nicht in der Zukunft. Vorschlag: Beginn + typische Dauer (Median der letzten 20 abgeschlossenen Einträge des Projekts, aufgerundet), sonst 18:00 am Tag des Beginns (Beginn danach: Beginn + 1 h), höchstens `abrunden(jetzt)`. Der Eintrag endet dort, aufgeteilt nach R14. Optional startet dieselbe Arbeit ab jetzt neu (wie ein Start, R1–R3). |

Eine Pause ist ein Stopp; die Lücke zwischen zwei Einträgen ist die Pause.

## Manuelle Einträge

- Anlegen, bearbeiten, löschen; Beginn/Ende im 5-Minuten-Raster
- Beginn < Ende; keine Überlappung mit anderen Einträgen (ein offener Eintrag belegt ab seinem Beginn alles)
- Einträge über Mitternacht werden beim Speichern aufgeteilt (R14)
- **Teilen** (Bearbeiten-Dialog): Ein abgeschlossener Eintrag wird an einem Rasterzeitpunkt strikt zwischen Beginn und
  Ende in zwei Einträge geteilt. Der zweite Teil kann direkt ein anderes Projekt/eine andere Tätigkeit bekommen.
  Offene Einträge können nicht geteilt werden – ihr Ende steht noch nicht fest; dafür gibt es den Wechsel.
- **Zusammenführen**: Zwei abgeschlossene Einträge mit Ende des ersten = Beginn des zweiten, gleichem Projekt und
  gleicher Tätigkeit werden zu einem Eintrag. Offene Einträge werden nicht zusammengeführt (siehe R3), und das Ergebnis
  darf keine lokale Mitternacht überspannen (R14).
  Die Tagesansicht weist auf solche Paare hin („Alle zusammenführen“, je Paar ein Symbol in der Eintragsliste);
  der Bearbeiten-Dialog bietet das Zusammenführen mit dem passenden Nachbarn an.

## Oberfläche

- **Timer-Leiste** fest oben auf jeder Seite: Projekt, Tätigkeit (editierbar, R13), laufende Dauer, on-hold-Hinweis
  („startet um 09:50“), Wechseln/Start und Stopp, Schnellwahl der zuletzt genutzten Projekt/Tätigkeit-Kombinationen,
  Warnung nach 10 h oder über Mitternacht mit Dialog „Ende nachtragen“ (R15, R16)
- **Browser-Tab** zeigt den Timer-Zustand: laufend „● 1:25 · Kunde / Projekt – Zeitwerk“ und Favicon als Punkt in
  Projektfarbe, on hold „⏸ ab 09:50 · Kunde / Projekt – Zeitwerk“ und Favicon als Ring mit Pause-Zeichen, im Leerlauf
  der normale Seitentitel und das Logo; aktualisiert minütlich und bei Zustandswechsel, vom Server gesteuert (stimmt
  auch in Hintergrund-Tabs)
- **Tagesansicht** (Startseite = heute): horizontale Zeitleiste in Projektfarben (Auto-Zoom, Standard 07–19 Uhr,
  erweitert sich bei Einträgen außerhalb), Jetzt-Linie, laufend/on hold gekennzeichnet, Tooltip, Klick öffnet
  Bearbeiten-Dialog; darunter Eintragsliste mit Tagessumme; Vor/Zurück-Navigation
- **Lücken** in der Tagesansicht (siehe unten)
- **Wochenansicht** (`/woche`, `/woche/2026-W43`): sieben Tages-Zeitleisten Mo–So untereinander mit gemeinsamer
  Zeitachse (Auto-Zoom über die ganze Woche, Standard 07–19 Uhr), Jetzt-Linie am heutigen Tag, Tagessummen rechts,
  Wochensumme und Summen je Projekt; Vor/Zurück/Heute-Navigation; Klick auf einen Eintrag öffnet den
  Bearbeiten-Dialog, Klick auf das Datum die Tagesansicht
- **Kalender**: Monatsansicht mit Tagessummen, Klick öffnet den Tag, Klick auf die KW öffnet die Woche; je Woche und
  im Kopf für den Monat Soll/Ist/Saldo (K6), im Kopf außerdem der Gesamtsaldo (Stand heute). Abwesenheiten sind
  farbig markiert und werden über das Menü (⋯) des Tages gesetzt oder entfernt.
- **Auswertung**: frei wählbarer Zeitraum (Von/Bis, je inklusive) mit Presets diese Woche (Mo–So), dieser Monat,
  letzter Monat, dieses Jahr (Standard: dieser Monat)
  - Summe im Zeitraum, im Vorzeitraum und die Veränderung (Delta in h und %)
  - Stunden pro Kunde und pro Projekt als Balken (Projekte in Projektfarbe) mit Tabelle: Stunden, Anteil in %, Delta
  - Top-Tätigkeiten (bis zu 10, je Projekt + Tätigkeit) nach Dauer; Einträge ohne Tätigkeit zählen hier nicht
  - Vorzeitraum: der gleich lange Zeitraum direkt davor; umfasst der Zeitraum ganze Monate, sind es ebenso viele
    ganze Kalendermonate (Oktober → September, Jahr → Vorjahr). Kunden/Projekte, die nur im Vorzeitraum Zeit haben,
    erscheinen mit 0 h und negativem Delta
  - Nur abgeschlossene Zeiteinträge zählen; ein Eintrag zählt zu dem lokalen Tag, auf den er fällt
    (Aufteilung an Mitternacht wie im Kalender, R14)
- **Stammdaten**: Kunden und Projekte verwalten, archivieren
- **Einstellungen**: Kontostart und Startsaldo; Versionen der Soll-Arbeitszeit anlegen, überschreiben (gleiches
  Gültig-ab), löschen
- **Export**: siehe unten
- Tätigkeit mit Autovervollständigung aus früheren Eingaben desselben Projekts

## Tastenkürzel

| Kürzel | Wirkung |
|--------|---------|
| `Strg+K` (Mac: `⌘K`) | Command-Palette öffnen |
| `S` | Stopp (R10–R12); ohne offenen Eintrag nur ein Hinweis |
| `←` / `→` | Vorheriger / nächster Tag (nur in der Tagesansicht) |
| `T` | Heute (Tagesansicht) |
| `?` | Hilfe-Overlay mit allen Kürzeln |
| `Esc` | Palette bzw. Dialog schließen |

- Die Kürzel greifen nicht, solange der Fokus in einem Eingabefeld (Input, Textarea, Select, editierbarer Bereich)
  liegt oder ein Dialog offen ist – auch `Strg+K` nicht.
- **Command-Palette**: unscharfe Suche (jeder Suchbegriff muss vorkommen, auch als Buchstabenfolge, z. B. „wbs“ →
  „Website“; zusammenhängende Treffer und Wortanfänge zuerst) über
  - die zuletzt genutzten Projekt/Tätigkeit-Kombinationen (ohne die gerade laufende),
  - die aktiven Projekte (ohne Tätigkeit),
  - Befehle: Stopp (nur bei offenem Eintrag), Heute, Gehe zu Kalender/Stammdaten/Export, Tastenkürzel anzeigen.
- Ohne Suchtext: zuletzt genutzt, dann Befehle, dann Projekte; höchstens 12 Treffer. `↑`/`↓` wählen, `Enter` führt aus.
- Ein Projekt oder eine Kombination löst Start bzw. Wechsel über den Timer aus – es gelten dieselben Regeln wie in
  der Timer-Leiste (R1–R9).

## Lücken

Eine Lücke ist unbelegte Zeit zwischen dem ersten und dem letzten Zeiteintrag eines Tages. Da Pausen legitim sind,
ist eine Lücke nur ein Hinweis, kein Fehler – es gibt keine Warnung und keine Pflicht, sie zu füllen.

- Vor dem ersten und nach dem letzten Eintrag des Tages gibt es keine Lücke.
- Ein offener Eintrag (laufend oder on hold) belegt ab seinem Beginn alles; vor ihm kann eine Lücke liegen, nach ihm nicht.
- Einträge, die über eine Tagesgrenze reichen, zählen nur mit ihrem Anteil am Tag; Lücken enden an der Tagesgrenze.
- Darstellung: in der Zeitleiste dezent schraffiert, in der Eintragsliste als eigene Zeile zwischen den Einträgen.
- Klick auf eine Lücke öffnet den Dialog für einen neuen Eintrag, vorbelegt mit Beginn und Ende der Lücke.
- Schnellaktion **„Lücke füllen mit …“** (vorheriges oder nächstes Projekt/Tätigkeit): verlängert den vorherigen
  Eintrag bis zum Ende der Lücke bzw. zieht den Beginn des nächsten Eintrags auf den Beginn der Lücke.
  Es entsteht kein zusätzlicher Eintrag.

## Ticket-Links

Erkannte Ticketnummern in Tätigkeiten werden als Link (neuer Tab) dargestellt; der restliche Text bleibt normal.

- Konfiguration in der App unter **Stammdaten** (in der Datenbank), nicht über Umgebungsvariablen:
  - **Ticket-URL**: Vorlage mit Platzhalter `{ticket}`, z. B. `https://jira.example.com/browse/{ticket}`;
    nur absolute `http`/`https`-URLs
  - **Ticket-Muster**: regulärer Ausdruck, Standard `[A-Z][A-Z0-9]+-\d+`; Groß-/Kleinschreibung zählt
    (mit `(?i)` abschaltbar)
- Ebene: am **Kunden**, am **Projekt** optional überschreibbar. Beide Felder werden einzeln aufgelöst:
  Projekt → Kunde → (nur Muster) Standard. Ohne Ticket-URL bei Projekt und Kunde gibt es keine Links.
- Treffer zählen nur als ganzes Wort (kein Buchstabe, keine Ziffer, kein `_` direkt davor oder danach):
  in `xABC-1` oder `ABC-1a` wird nichts verlinkt.
- Die Ticketnummer wird URL-kodiert eingesetzt; die Tätigkeit bleibt immer Text (kein HTML). Ungültige Vorlagen
  oder Muster werden beim Speichern abgelehnt; ein Muster, das länger als 100 ms rechnet, führt zu reinem Text.
- Anzeige mit Links: Eintragsliste der Tagesansicht, Beschriftung der Blöcke in der Zeitleiste, Timer-Leiste
  (laufender Eintrag und Schnellwahl). Klick auf einen Link öffnet nicht zusätzlich den Eintrag bzw. wechselt nicht.
  Der native Tooltip der Zeitleiste kann keine Links enthalten und bleibt Text; der Kalender zeigt keine Tätigkeiten.
- Der CSV-Export bleibt unverändert (Tätigkeit als Text).

## CSV-Export

- Einzelbuchungen, Filter: Zeitraum (Pflicht), Kunde/Projekt (optional); nur abgeschlossene Einträge
- `;` als Trenner, UTF-8 **ohne** BOM, Dezimalkomma
- Spalten: `Datum;Start;Ende;Dauer;Stunden;Kunde;Projekt;Tätigkeit` – Dauer als `hh:mm`, Stunden als `1,75`
- Wahlweise pro Tag summiert: eine Zeile je Tag (lokale Zeit), Kunde und Projekt, sortiert nach Datum, Kunde, Projekt
  - Spalten: `Datum;Dauer;Stunden;Kunde;Projekt;Tätigkeiten` – Tätigkeiten ohne Duplikate, mit `, ` verbunden,
    in Reihenfolge ihres ersten Auftretens

## Bewusst später

Weitere Aggregationen (z. B. pro Woche/Monat), Drag & Drop in der Zeitleiste, Abrechenbar-Flag.

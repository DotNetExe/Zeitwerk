# Zeitwerk

Zeitwerk ist eine Einzelplatz-Zeiterfassung, in der eine Person ihre Arbeitszeit fließend zwischen Projekten umbucht. Alle Zeiten liegen auf einem festen Fünf-Minuten-Raster.

## Language

### Stammdaten

**Kunde**:
Auftraggeber, dem Projekte zugeordnet sind.
_Avoid_: Client, Mandant, Firma

**Projekt**:
Eine Arbeit für genau einen Kunden, auf die Zeit gebucht wird. Jedes Projekt hat eine eigene Farbe.
_Avoid_: Auftrag, Job

**Archiviert**:
Zustand eines Kunden oder Projekts, das nicht mehr ausgewählt werden kann, dessen gebuchte Zeiten aber erhalten bleiben.
_Avoid_: gelöscht, inaktiv

### Zeiterfassung

**Zeiteintrag**:
Ein zusammenhängender Zeitraum, der auf ein Projekt (und optional eine Tätigkeit) gebucht ist.
_Avoid_: Buchung, Record, Log

**Tätigkeit**:
Optionaler Freitext an einem Zeiteintrag, meistens eine Ticketnummer.
_Avoid_: Task, Notiz, Beschreibung

**Ticket-Link**:
Link auf das Ticketsystem, der aus einer in der Tätigkeit erkannten Ticketnummer und der Ticket-URL des Projekts bzw. Kunden entsteht.
_Avoid_: Issue-Link, Jira-Link

**Raster**:
Die Fünf-Minuten-Einteilung der Uhr (:00, :05, :10, …), auf der jeder Beginn und jedes Ende eines Zeiteintrags liegt.
_Avoid_: Takt, Intervall

**Slot**:
Ein einzelnes Fünf-Minuten-Stück des Rasters.

**Toleranz**:
Die Zeitspanne nach einer Rastergrenze, innerhalb der ein Wechsel oder Stopp noch auf diese Grenze statt auf die nächste fällt.
_Avoid_: Kulanz, Gnadenfrist

### Timer

**Timer**:
Die laufende Erfassung; es gibt höchstens einen offenen Zeiteintrag.

**Laufend**:
Ein offener Zeiteintrag, dessen Beginn erreicht ist.

**On hold**:
Ein offener Zeiteintrag, dessen Beginn noch in der Zukunft liegt, weil der vorherige Zeiteintrag bis zur nächsten Rastergrenze aufgerundet wurde.
_Avoid_: wartend, geplant, pending

**Start**:
Beginn der Erfassung aus dem Leerlauf; der Beginn wird auf das Raster abgerundet.

**Wechsel**:
Übergang des Timers zu einem anderen Projekt oder einer anderen Tätigkeit; der bisherige Zeiteintrag endet an der aufgerundeten Rastergrenze, an der der neue beginnt.
_Avoid_: Umbuchen, Switch

**Stopp**:
Beenden des Timers; das Ende wird auf das Raster aufgerundet. Eine Pause ist ein Stopp mit späterem Start.
_Avoid_: Pause (als eigener Zustand)

**Fortsetzen**:
Ein Start mit gleichem Projekt und gleicher Tätigkeit, der direkt an den letzten Zeiteintrag anschließt und diesen wieder öffnet, statt einen neuen anzulegen.

### Überstundenkonto

**Überstundenkonto**:
Die laufende Gegenüberstellung von Soll und Ist ab dem Kontostart; sein Stand ist der Saldo.
_Avoid_: Gleitzeitkonto, Arbeitszeitkonto, Zeitkonto

**Soll**:
Die Arbeitszeit, die an einem Tag laut Soll-Arbeitszeit erwartet wird; an Abwesenheitstagen 0.
_Avoid_: Sollzeit, Pflichtzeit, Target

**Ist**:
Die an einem Tag tatsächlich gebuchte Zeit (Summe der Zeiteinträge).
_Avoid_: Istzeit, Arbeitszeit

**Soll-Arbeitszeit**:
Das Soll je Wochentag, in Versionen mit Gültig-ab-Datum; eine Version gilt bis zur nächsten.
_Avoid_: Arbeitszeitmodell, Wochenplan

**Gültig ab**:
Der erste Tag, an dem eine Version der Soll-Arbeitszeit gilt.

**Abwesenheit**:
Ein ganzer Tag Urlaub, Krankheit oder Feiertag, an dem das Soll 0 ist.
_Avoid_: Fehltag, Freizeit

**Kontostart**:
Der erste Tag, der zum Überstundenkonto zählt, zusammen mit dem Startsaldo, z. B. übernommenen Überstunden.

**Saldo**:
Startsaldo plus Ist minus Soll aller Tage vom Kontostart bis heute; positiv = Überstunden, negativ = Minusstunden.
_Avoid_: Überstunden (für den Kontostand), Bilanz

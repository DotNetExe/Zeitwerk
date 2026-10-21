<p><img src="src/Zeitwerk.Web/wwwroot/wordmark.svg" alt="Zeitwerk" height="56"></p>

# Zeitwerk

Einzelplatz-Zeiterfassung mit fließendem Projektwechsel im 5-Minuten-Raster.
Fachliche Regeln: [SPEC.md](SPEC.md) · Begriffe: [CONTEXT.md](CONTEXT.md)

## Betrieb mit Docker Compose

```bash
cp .env.example .env   # optional, Standardwerte funktionieren ohne
docker compose up -d --build
```

Danach läuft die App unter <http://127.0.0.1:8080> (nur lokal erreichbar).

- Datenbank: PostgreSQL 17 im Volume `zeitwerk-db`; Migrationen laufen beim App-Start automatisch.
- Backups: Der Dienst `backup` schreibt täglich `./backups/zeitwerk_<datum>.sql.gz` und behält die letzten 14.
- Wiederherstellen:
  ```bash
  gunzip -c backups/zeitwerk_2026-10-21_0800.sql.gz | docker compose exec -T db psql -U zeitwerk -d zeitwerk
  ```
- Konfiguration über `.env`: Port, Zeitzone, Toleranz in Sekunden, DB-Passwort, Anzahl Backups.

## Entwicklung

Voraussetzungen: .NET 10 SDK und Docker. Die Entwicklungsdatenbank läuft separat vom Betriebs-Stack
(eigenes Compose-Projekt `zeitwerk-dev`, eigenes Volume) auf `127.0.0.1:5433`; `appsettings.Development.json`
und `dotnet ef` sind darauf eingestellt.

```bash
docker compose -f docker-compose.dev.yml up -d
dotnet test
dotnet run --project src/Zeitwerk.Web
dotnet tool restore && dotnet ef migrations add <Name> -p src/Zeitwerk.Web -o Data/Migrations
```

Aufbau:

- `src/Zeitwerk.Web/Domain` – Zeitlogik ohne Datenbank (Raster, Timer-Engine, Tagesaufteilung, Prüfregeln)
- `src/Zeitwerk.Web/Services` – Datenbankzugriff, Timer-Aktionen, CSV-Export
- `src/Zeitwerk.Web/Components` – Blazor-Oberfläche (MudBlazor, Dark-Theme)
- `tests/Zeitwerk.Tests` – xUnit-Tests; die Regeln R1–R14 aus der SPEC sind einzeln abgedeckt

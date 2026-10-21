#!/bin/sh
# Erstellt einmal täglich einen komprimierten pg_dump und behält die letzten $BACKUP_KEEP Dateien.
set -eu

while true; do
    file="/backups/zeitwerk_$(date +%Y-%m-%d_%H%M).sql.gz"
    if pg_dump --clean --if-exists | gzip > "$file.tmp"; then
        mv "$file.tmp" "$file"
        echo "Backup erstellt: $file"
    else
        rm -f "$file.tmp"
        echo "Backup fehlgeschlagen" >&2
    fi
    ls -1t /backups/zeitwerk_*.sql.gz 2>/dev/null | tail -n +"$((BACKUP_KEEP + 1))" | xargs -r rm -f
    sleep 86400
done

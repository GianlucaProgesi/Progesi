# Operazioni & Manutenzione (SQLite)

## Backup
- **A caldo** (WAL attivo): copia `progesi.sqlite` + eventuali `progesi.sqlite-wal` e `progesi.sqlite-shm`.
- **A freddo**: fermare processi che scrivono, poi copiare il `.sqlite`.

## Integrità
- `PRAGMA integrity_check;` → deve restituire `ok`.
- `PRAGMA wal_checkpoint(TRUNCATE);` per consolidare il WAL.

## Compattazione
- `VACUUM;` riduce dimensione del file dopo molte delete/update. Consigliato periodico (es. mensile).

## Concorrenza
- Abilitato **WAL** e `busy_timeout=5000ms`.
- I repository applicano **retry con backoff** su `SQLITE_BUSY/LOCKED`.

## Deduplica & Schema
- Indice `UNIQUE(ContentHash)` su tabelle principali.
- Dedup “di recupero” mantiene l’**Id** più basso per ciascun contenuto all’avvio.

## Logging
- Logger iniettabili: `TraceLogger`, `FileLogger`, `RollingFileLogger`.

## Tool CLI
Vedi `tools/ProgesiDbMaint`:
- `stats <db>` – conteggi e dimensione file
- `integrity <db>` – `PRAGMA integrity_check`
- `vacuum <db>` – compattazione
- `checkpoint <db>` – `wal_checkpoint(FULL)`
- `dedup <db> <table>` – rimuove duplicati mantenendo MIN(Id)

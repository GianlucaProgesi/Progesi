-- p7_variables_migrate.sql
PRAGMA foreign_keys=OFF;
BEGIN;

CREATE TABLE IF NOT EXISTS variables(
  Id              INTEGER PRIMARY KEY,
  Hash            TEXT    NOT NULL,
  Name            TEXT    NOT NULL,
  Value           TEXT    NOT NULL,
  Unit            TEXT    NOT NULL,
  By              TEXT    NOT NULL,
  LastModifiedUtc TEXT    NOT NULL
);

COMMIT;
PRAGMA foreign_keys=ON;

-- SEED di esempio (opzionale)
-- INSERT OR REPLACE INTO variables(Id,Hash,Name,Value,Unit,By,LastModifiedUtc) VALUES
-- (1,'hash-1','LEN','100','mm','GM','2025-09-29 01:00:00'),
-- (2,'hash-2','WID','50','mm','GM','2025-09-29 01:00:00');

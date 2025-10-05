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

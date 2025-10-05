PRAGMA foreign_keys=OFF;
BEGIN;

-- Rinomina la tabella corrente (esiste già: 'metadata')
ALTER TABLE metadata RENAME TO _metadata_old;

-- Crea lo schema corretto
CREATE TABLE metadata (
  Id              INTEGER PRIMARY KEY,
  Hash            TEXT    NOT NULL DEFAULT '',
  By              TEXT    NOT NULL DEFAULT '',
  Ref             TEXT    NOT NULL DEFAULT '',
  Snips           TEXT    NOT NULL DEFAULT '',
  LastModifiedUtc TEXT    NOT NULL DEFAULT (datetime('now'))
);

-- Ricopia i dati disponibili dalla tabella vecchia
INSERT INTO metadata(Id,Hash,By,Ref,Snips,LastModifiedUtc)
SELECT
  Id,
  COALESCE(Hash,''),
  ''  AS By,
  ''  AS Ref,
  ''  AS Snips,
  COALESCE(LastModifiedUtc, datetime('now'))
FROM _metadata_old;

DROP TABLE IF EXISTS _metadata_old;

COMMIT;
PRAGMA foreign_keys=ON;

-- Seed opzionale (per allineare ai mock)
INSERT OR REPLACE INTO metadata(Id,Hash,By,Ref,Snips,LastModifiedUtc) VALUES
(1,'mock-00000001','GM','https://example.org/metadata/1','snip:1:image/png:caption=Mock-1','2025-09-29 01:00:00'),
(2,'mock-00000002','GM','https://example.org/metadata/2','snip:2:image/png:caption=Mock-2','2025-09-29 01:00:00'),
(3,'mock-00000003','GM','https://example.org/metadata/3','snip:3:image/png:caption=Mock-3','2025-09-29 01:00:00');

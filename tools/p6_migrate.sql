PRAGMA foreign_keys=OFF;
BEGIN;

-- Se esiste già una tabella con schema “vecchio”, la rinominiamo.
DROP TABLE IF EXISTS _Metadata_old;
CREATE TABLE IF NOT EXISTS Metadata (
  Id              INTEGER PRIMARY KEY,
  Hash            TEXT    NOT NULL DEFAULT '',
  By              TEXT    NOT NULL DEFAULT '',
  Ref             TEXT    NOT NULL DEFAULT '',
  Snips           TEXT    NOT NULL DEFAULT '',
  LastModifiedUtc TEXT    NOT NULL DEFAULT (datetime('now'))
);

-- Se la vecchia tabella c’è, sposta dentro quello che riesci.
-- (Se non esiste, questi comandi non faranno nulla.)
SELECT name FROM sqlite_master WHERE name='Metadata_old';
-- Se hai una tabella “Metadata” con schema errato, rimuovi le righe qui sopra e usa:
--   ALTER TABLE Metadata RENAME TO _Metadata_old;
--   (ri-crea la CREATE TABLE Metadata di cui sopra)
--   INSERT INTO Metadata(Id,Hash,By,Ref,Snips,LastModifiedUtc)
--     SELECT Id, COALESCE(Hash,''), '', '', '', COALESCE(LastModifiedUtc, datetime('now'))
--     FROM _Metadata_old;
--   DROP TABLE _Metadata_old;

COMMIT;
PRAGMA foreign_keys=ON;

-- --- SEED opzionale (allinea ai mock 1..3)
INSERT OR REPLACE INTO Metadata(Id,Hash,By,Ref,Snips,LastModifiedUtc) VALUES
(1,'mock-00000001','GM','https://example.org/metadata/1','snip:1:image/png:caption=Mock-1','2025-09-29 01:00:00'),
(2,'mock-00000002','GM','https://example.org/metadata/2','snip:2:image/png:caption=Mock-2','2025-09-29 01:00:00'),
(3,'mock-00000003','GM','https://example.org/metadata/3','snip:3:image/png:caption=Mock-3','2025-09-29 01:00:00');

-- progesi_seed.sql  (crea schema + dati di esempio)
PRAGMA journal_mode=WAL;

CREATE TABLE IF NOT EXISTS variables (
  Id              INTEGER PRIMARY KEY,
  Hash            TEXT,
  Name            TEXT,
  Value           TEXT,
  Unit            TEXT,
  By              TEXT,
  LastModifiedUtc TEXT,
  Info            TEXT
);

CREATE TABLE IF NOT EXISTS metadata (
  Id              INTEGER PRIMARY KEY,
  Hash            TEXT,
  By              TEXT,
  Refs            TEXT,   -- stringa con ; come separatore oppure JSON (entrambi accettabili)
  Snips           TEXT,   -- stringa (snip:...) o JSON
  LastModifiedUtc TEXT,
  Info            TEXT
);

CREATE INDEX IF NOT EXISTS idx_variables_hash ON variables(Hash);
CREATE INDEX IF NOT EXISTS idx_metadata_hash  ON metadata(Hash);

-- ISO UTC di comodo
WITH lm(x) AS (SELECT '2025-10-12T00:00:00' )
INSERT INTO variables(Id, Hash, Name, Value, Unit, By, LastModifiedUtc, Info)
VALUES
  (1, 'v_9a63c8f2a1e4a3d0', 'Length', '100.0', 'mm', 'beta-user', (SELECT x FROM lm), 'seed var'),
  (2, 'v_934d1af26b6bcb12', 'Width',  '35.5',  'mm', 'beta-user', (SELECT x FROM lm), 'seed var');

WITH lm(x) AS (SELECT '2025-10-12T00:00:00' )
INSERT INTO metadata(Id, Hash, By, Refs, Snips, LastModifiedUtc, Info)
VALUES
  (1, 'm_2b3d9f8a11a4c7e1', 'beta-user',
     'https://example.com/specs/part-01; data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHVgL1kK2A7gAAAABJRU5ErkJggg==',
     'snip:1:image/png:caption=part 01',
     (SELECT x FROM lm), 'seed metadata'),
  (2, 'm_1c4f8a0b22d5e6f7', 'beta-user',
     'https://example.com/specs/part-02', '', (SELECT x FROM lm), 'seed metadata');

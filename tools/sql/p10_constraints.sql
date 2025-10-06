
-- Indici base per query rapide su Hash
CREATE INDEX IF NOT EXISTS idx_metadata_hash ON metadata(Hash);
CREATE INDEX IF NOT EXISTS idx_variables_hash ON variables(Hash);

-- Aggiunta timestamp LastModifiedUtc (idempotente via try/catch a livello di script runner)
ALTER TABLE metadata ADD COLUMN LastModifiedUtc TEXT;
ALTER TABLE variables ADD COLUMN LastModifiedUtc TEXT;

-- Trigger per aggiornare LastModifiedUtc
CREATE TRIGGER IF NOT EXISTS trg_metadata_lastmod_set
AFTER INSERT ON metadata
FOR EACH ROW
WHEN NEW.LastModifiedUtc IS NULL
BEGIN
  UPDATE metadata SET LastModifiedUtc = strftime('%Y-%m-%dT%H:%M:%SZ','now') WHERE rowid = NEW.rowid;
END;

CREATE TRIGGER IF NOT EXISTS trg_metadata_lastmod_update
AFTER UPDATE ON metadata
FOR EACH ROW
BEGIN
  UPDATE metadata SET LastModifiedUtc = strftime('%Y-%m-%dT%H:%M:%SZ','now') WHERE rowid = NEW.rowid;
END;

CREATE TRIGGER IF NOT EXISTS trg_variables_lastmod_set
AFTER INSERT ON variables
FOR EACH ROW
WHEN NEW.LastModifiedUtc IS NULL
BEGIN
  UPDATE variables SET LastModifiedUtc = strftime('%Y-%m-%dT%H:%M:%SZ','now') WHERE rowid = NEW.rowid;
END;

CREATE TRIGGER IF NOT EXISTS trg_variables_lastmod_update
AFTER UPDATE ON variables
FOR EACH ROW
BEGIN
  UPDATE variables SET LastModifiedUtc = strftime('%Y-%m-%dT%H:%M:%SZ','now') WHERE rowid = NEW.rowid;
END;

-- Whitelist per Ref: http/https/data, blocco esplicito di file:
CREATE TRIGGER IF NOT EXISTS trg_metadata_ref_whitelist
BEFORE INSERT ON metadata
FOR EACH ROW
WHEN NEW.Ref IS NOT NULL AND NOT (
  NEW.Ref LIKE 'http://%' OR NEW.Ref LIKE 'https://%' OR NEW.Ref LIKE 'data:%'
)
BEGIN
  SELECT RAISE(ABORT,'Invalid Ref: schema non ammesso (solo http, https, data)');
END;

CREATE TRIGGER IF NOT EXISTS trg_variables_ref_whitelist
BEFORE INSERT ON variables
FOR EACH ROW
WHEN NEW.Ref IS NOT NULL AND NOT (
  NEW.Ref LIKE 'http://%' OR NEW.Ref LIKE 'https://%' OR NEW.Ref LIKE 'data:%'
)
BEGIN
  SELECT RAISE(ABORT,'Invalid Ref: schema non ammesso (solo http, https, data)');
END;

CREATE TRIGGER IF NOT EXISTS trg_metadata_ref_no_file
BEFORE INSERT ON metadata
FOR EACH ROW
WHEN NEW.Ref LIKE 'file:%'
BEGIN
  SELECT RAISE(ABORT,'Invalid Ref: file: non consentito');
END;

CREATE TRIGGER IF NOT EXISTS trg_variables_ref_no_file
BEFORE INSERT ON variables
FOR EACH ROW
WHEN NEW.Ref LIKE 'file:%'
BEGIN
  SELECT RAISE(ABORT,'Invalid Ref: file: non consentito');
END;

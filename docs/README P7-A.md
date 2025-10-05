📘 P7-A – Metadata CSV Import/export

✅ Obiettivi

Abilitare import/export CSV per la tabella metadata in SQLite.

Garantire compatibilità con i componenti ProgesiMetadataIn/Out in GH.

Integrare un flusso stabile di backup/restore (con Backup-VacuumSqlite.ps1).

🔧 Tooling

Export-MetadataCsv.ps1
Esporta i record dalla tabella metadata in un file CSV (UTF-8).

.\tools\Export-MetadataCsv.ps1 -Db ".\tests\P6-live\progesi_p6.db" -Csv ".\export\metadata.csv"


Import-MetadataCsv.ps1
Importa i record da CSV nel database.

.\tools\Import-MetadataCsv.ps1 -Db ".\tests\P6-live\progesi_p6.db" -Csv ".\export\metadata.csv"


Backup-VacuumSqlite.ps1
Esegue un backup compatto del DB prima di operazioni critiche.

.\tools\Backup-VacuumSqlite.ps1 -Db ".\tests\P6-live\progesi_p6.db"

🧪 Test Mock in Grasshopper

Creazione (ProgesiMetadataIn con Act=Create) → record inserito.

Update (Act=Update) → update correttamente propagato.

Read (ProgesiMetadataOut) → valori coerenti con SQLite.

Snapshot confermato ✅.

🔑 Hash Code Strategy

Ogni record mantiene un hash deterministico calcolato concatenando proprietà chiave.

ProgesiMetadata:

hash = $"{Id}+{CreatedBy}+{LastModified}+{AdditionalInfo}"


ProgesiVariable:

hash = $"{Id}+{Name}+{Value}"

📌 Note

Tutti gli script PowerShell sono stati validati in PowerShell 5.1+.

CSV encoding forzato in UTF-8 senza BOM per compatibilità.

L’infrastruttura GH utilizza ora Live Mode (SQLite) senza fallback su mock.


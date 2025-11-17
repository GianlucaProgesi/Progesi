
---

### `docs/TROUBLESHOOTING.md`

```markdown
# Troubleshooting

Questa sezione raccoglie i problemi piu' comuni e le relative soluzioni.

## 1) "MethodCallTranslator" exception (solo EF in-proc)

Sintomo:
- In GH, `ExportEf` o `ImportEf` mostra "The type initializer for 'MethodCallTranslator' threw an exception".
- Il DB rimane a 0 KB.

Cause tipiche:
- Le DLL `System.Data.SQLite.Linq.dll` o `SQLite.Interop.dll` non sono accanto alla `.gha`.
- Il provider EF6 per SQLite non trova le proprie dipendenze a runtime.

Soluzione:
- Con la versione "pure SQLite" del plugin **non usiamo piu' EF in-process**. I rami `ExportEf/ImportEf` sono mappati a SQLite e funzionano senza EF.
- Se vuoi usare EF fuori da GH, usa il tool `Progesi.EF.Tool` (opzionale), che porta con se' le sue dipendenze.

## 2) "SQLite Error 19: FOREIGN KEY constraint failed" in ExportSqlite

Causa:
- Nella variabile `Variables.MetaId` e' presente un valore non presente in `Metadata.Id`.
- In passato si e' inserito `0` anziche' `NULL` per MetaId.

Soluzione:
- Assicurati di usare la versione aggiornata del plugin (S2-B/S2-C) che imposta `MetaId=NULL` quando il metadato non esiste.
- Oppure correggi i dati in RHINO: crea prima i metadati necessari, poi le variabili.

## 3) "SQLite schema not found (Metadata/Variables)" in ImportSqlite

Causa:
- Stai importando da un DB vuoto o da un export interrotto (file copiato a meta' elaborazione).

Soluzione:
- Elimina il file .db e rifai `ExportSqlite` per generare da capo il DB, poi `ImportSqlite`.

## 4) Balloon rossi appena posiziono il componente in canvas

Causa:
- Sono rimasti valori persistenti di una versione precedente con piu' input o di tipo diverso.
- Mismatch di tipo tra input Panel e porta del componente.

Soluzione:
- Elimina il componente dal canvas e reinseriscilo.
- Oppure click destro sulla porta input -> "Disconnect" o "Clear".
- Imposta tutti gli input non essenziali come Optional (gia' fatto nel codice).

## 5) "Accesso negato" o file bloccato

Causa:
- Il file .db e' aperto in un altro processo o non hai permessi di scrittura nella cartella di destinazione.

Soluzione:
- Chiudi applicazioni che tengono aperto il file.
- Scegli una cartella come `C:\Temp` o esegui Rhino con privilegi adeguati.
- Lo script di export tenta di cancellare il file esistente; se non riesce, salva con suffisso `_yyyyMMdd_HHmmss.db`.

## 6) Dove finisce il log?

- Gli import generano `nomefile.db.import.log.txt` accanto al DB (o `nomefile.xlsx.import.log.txt` per Excel).
- Se `Dry=true`, il file di log viene comunque scritto ma senza modifiche al repo RHINO.

## 7) Come verificare rapidamente?

- `Act="ExportSqlite"`, `Path="C:\Temp\check.db"`, `Run=TRUE`  
  Poi `Act="ImportSqlite"` sullo stesso file.
- In caso di problemi, controlla `Warn`/`Err` e il file `.import.log.txt`.


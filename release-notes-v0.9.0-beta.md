# Progesi v0.9.0-beta

Questa versione conclude la Beta con:
- Plugin GH "pure SQLite": niente EF in-process, export/import stabili su Excel e SQLite.
- Strict/Lenient, Preview (DryRun), ErrRC (coordinate errori), log `*.import.log.txt`.
- Compatibilita' garantita con Rhino 8.20.25157.13001.
- Icone componenti ripristinate (embedded).

## Cosa c'e' nello zip
- `ProgesiGrasshopperAssembly.gha` + dipendenze managed richieste (ClosedXML, Newtonsoft, SQLite provider).
- `docs/` (Deploy, Troubleshooting, CI) e `README.md`.

## Note
- Gli act `ExportEf/ImportEf` sono alias di SQLite nel plugin GH.
- Per scenari EF fuori da GH: usare il progetto `Progesi.Data.EF` o il tool `Progesi.EF.Tool`.

Grazie per i test e i feedback durante la Beta!

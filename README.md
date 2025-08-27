# Progesi

![CI](https://github.com/GianlucaProgesi/Progesi/actions/workflows/ci.yml/badge.svg)

Soluzione per gestione **Variables** e **Metadata** con repository **SQLite** e integrazione **Rhino/Grasshopper**.  
La versione corrente compila e **passa tutti i test**.

---

## Contenuti

- [`src/ProgesiCore`](src/ProgesiCore)  
  Value objects, `ProgesiMetadata`, `ProgesiVariable`, hashing (`ProgesiHash`).
- [`src/ProgesiRepositories.Sqlite`](src/ProgesiRepositories.Sqlite)  
  Repository SQLite per **Variables** e **Metadata**.
- [`src/ProgesiRepositories.Rhino`](src/ProgesiRepositories.Rhino)  
  Repository basato su Rhino doc/user strings (per integrazione GH).
- [`src/ProgesiGrasshopperAssembly`](src/ProgesiGrasshopperAssembly)  
  Componenti “smoke” per test manuali da Grasshopper.
- [`tests/ProgesiRepositories.Sqlite.Tests`](tests/ProgesiRepositories.Sqlite.Tests)  
  Test xUnit (dedup, round-trip, concorrenza).

---

## Requisiti

- **Windows + Visual Studio 2022**
- **.NET Framework 4.8 Developer Pack**
- **NuGet restore** abilitato
- (per compilare l’assembly GH) **Rhino + Grasshopper** installati e referenze risolte localmente

---

## Quick start

### Build & Test (VS)
1. Apri la soluzione `Progesi.sln`
2. **Restore** NuGet
3. Compila *Debug*
4. Esegui i test (Test Explorer)

### Build & Test (CLI)
```powershell
nuget restore Progesi.sln
msbuild Progesi.sln /p:Configuration=Debug /m
dotnet test Progesi.sln --configuration Debug --no-build --logger "trx;LogFileName=test-results.trx"


Uso rapido (SQLite)

using ProgesiCore;
using ProgesiRepositories.Sqlite;

// Percorso DB
var repo = new SqliteMetadataRepository(dbPath: @"C:\temp\progesi.sqlite");

// Creo un metadata
var snip = ProgesiSnip.Create(
    content: new byte[] { 0x01, 0x02, 0x03 },
    mimeType: "image/png",
    caption: "cap");

var meta = ProgesiMetadata.Create(
    createdBy: "usr",
    additionalInfo: "info",
    references: new[] { new Uri("https://a/"), new Uri("https://b/") },
    snips: new[] { snip });

// Upsert con dedup per contenuto
await repo.UpsertAsync(meta);

// Lettura
var all = await repo.ListAsync(); // non contiene duplicati di contenuto
Semantica di UpsertAsync (Metadata, SQLite)

Calcola ContentHash identico a ProgesiHash.Compute.

Tabella Metadata con indice UNIQUE(ContentHash).

UpsertAsync:

se esiste lo stesso contenuto, non crea una nuova riga; aggiorna solo LastModified;

se è contenuto nuovo e l’oggetto ha Id > 0, la riga viene creata con quell’Id (utile per round-trip).

GetAsync(id) ricostruisce dal DB usando Id e LastModified delle colonne, non quelli nel JSON.

CI

GitHub Actions su Windows: restore → build (Debug) → test.

File workflow: .github/workflows/ci.yml

I risultati TRX vengono pubblicati come artifact.

Suggerito: mantenere solo Squash and merge su main.

Badge CI nel README (già incluso).

Convenzioni & Qualità

Nullable abilitato, TreatWarningsAsErrors = true

.gitignore esclude bin/, obj/, artefatti locali, DB .sqlite

Commit: preferibile conventional commits (feat:, fix:, chore:, ci:…)

Branching:

feature branches → PR verso main

nome consigliato: feature/<descrizione>, fix/<descrizione>, stabilize/<data>

Roadmap (breve)

Variabili: scenari avanzati (deps cicliche, valutazioni lazy, caching).

Repositories: test d’integrazione multi-processo su SQLite.

Grasshopper: componenti “end-user” oltre agli smoke tests.

Documentazione API (XML doc + README per progetto).

Note Rhino/Grasshopper

Il progetto ProgesiGrasshopperAssembly compila contro Rhino/Grasshopper installati sul sistema.
Assicurati che le referenze RhinoCommon.dll/Grasshopper.dll siano risolte (tipicamente in C:\Program Files\Rhino...).

Licenza

Inserisci qui la licenza del progetto (es. MIT) oppure specifica che il codice è proprietario.
Esempio MIT: crea un file LICENSE con il testo e cita qui “MIT License”.

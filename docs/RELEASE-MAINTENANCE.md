# 🛠️ Progesi — Release Maintenance Checklist

Questa lista aiuta a mantenere sano e prevedibile il flusso di rilascio (CI/CD, pacchetti, changelog).

## 1) Ogni mese (o dopo variazioni importanti)
- [ ] **Aggiorna SDK .NET** nel workflow (`actions/setup-dotnet@v4`, `8.x` → ultimo LTS)
- [ ] **Aggiorna Actions** (checkout/setup-dotnet/upload/download-artifact/gh-release) a minor versione sicura
- [ ] **Verifica MinVer**: `Directory.Build.props` allineato (tag prefix `v`, auto-increment `patch`)
- [ ] **Controlla quality gates**:
  - [ ] `snupkg` generati
  - [ ] `README.md` *incluso* nei `.nupkg`
  - [ ] **SourceLink** valido (`dotnet sourcelink test`)
- [ ] **Changelog**: convenzioni dei commit rispettate (feat/fix/docs/chore/refactor/test, breaking)
- [ ] **Badge/README**: link e badge funzionanti (build, version, downloads, GPR)

## 2) Ogni trimestre
- [ ] **Segreti e token**
  - [ ] `NUGET_API_KEY` attivo e non in scadenza
  - [ ] `GPR_PAT` (se usato) con scope minimo (`read:packages` lato consumo; `write:packages` solo dove serve)
  - [ ] **Rotazione** PAT/API key (policy consigliata: 6–12 mesi)
- [ ] **Protezione branch**: regole su `main` (PR required, status checks, no force-push)
- [ ] **Retention**: artifact retention e log *ufficiali* (impostazioni GitHub Actions)
- [ ] **Sicurezza**
  - [ ] Dependabot attivo (actions & nuget)
  - [ ] Code scanning / CodeQL (se rilevante)
  - [ ] Avvisi di sicurezza risolti
- [ ] **Licenze & metadata**: licenza corretta in repo e nei `.csproj` (LicenseExpression, RepositoryUrl, SourceLink)

## 3) Pre-rilascio (checklist rapida)
- [ ] Working tree pulito (`git status`)  
- [ ] Test verdi (`dotnet test`)  
- [ ] Commit messaggi **convenzionali**  
- [ ] `End-to-End-Release.ps1` → **DryRun** ok  
- [ ] Se *pre-release*: usa `-Pre 'beta.1'` (o canale concordato)

## 4) Rilascio (one-liner)
- [ ] `pwsh -File ./tools/End-to-End-Release.ps1`
  - genera/committa changelog
  - crea & pusha `vX.Y.Z` (o pre-release)
  - **CI** pubblica su NuGet + GPR e crea la Release

## 5) Post-rilascio (Smoke check)
- [ ] NuGet.org: tutti i pacchetti visibili e installabili (`dotnet add package ...`)
- [ ] GPR (se usato): feed accessibile con PAT minimo
- [ ] Release GitHub: asset `.nupkg` corretti, changelog presente
- [ ] README root & per-project aggiornati (badge, install, quick start)

---

## 🔍 Troubleshooting veloce
- **Errore YAML linea N**: copia *integrale* del `release.yml` “golden” e riprova.
- **NU5039 readme mancante**: assicurati `PackageReadmeFile=README.md` nei progetti e file presente in cartella.
- **SourceLink fallisce**: verifica `Microsoft.SourceLink.GitHub` e che il commit sia *pushato/public*.
- **Versione sbagliata**: controlla tag `v*` e MinVer (`MinVerTagPrefix=v`).

---

## 🧰 Strumenti utili (già nel repo)
- `tools/End-to-End-Release.ps1` — un comando e rilasci
- `tools/Release-Next.ps1` — calcola e pusha il prossimo tag dai commit
- `tools/Build-Changelog.ps1` — genera changelog coerente (UTF-8 no BOM)
- `tools/Verify-Packages.ps1` — quality gates su readme/snupkg/sourcelink
- `tools/Release-QuickRef.ps1` — promemoria + stato pacchetti su NuGet

---

## ✅ Policy consigliate
- **SemVer**: major per breaking, minor per feat, patch per fix/altro
- **Conventional Commits**: `feat:`, `fix:`, `docs:`, `chore:`, `refactor:`, `test:`, `perf:`
- **Tag**: sempre `vX.Y.Z` (MinVer richiede `v`)


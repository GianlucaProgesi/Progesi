<!-- PROGESI:HERO:START -->
<p align="center">
  <img src="docs/assets/progesi-logo.jpg" alt="Progesi Logo" width="400"/>
</p>

<h1 align="center">Progesi Engineering Toolchain</h1>

<p align="center">
  <a href="https://github.com/GianlucaProgesi/Progesi/actions/workflows/release.yml"><img src="https://github.com/GianlucaProgesi/Progesi/actions/workflows/release.yml/badge.svg" alt="Build"/></a>
  <a href="https://www.nuget.org/packages/ProgesiCore"><img src="https://img.shields.io/nuget/v/ProgesiCore.svg" alt="NuGet"/></a>
  <a href="https://www.nuget.org/packages/ProgesiCore"><img src="https://img.shields.io/nuget/dt/ProgesiCore.svg" alt="Downloads"/></a>
  <a href="tools/Release-HealthCheck.ps1"><img src="https://img.shields.io/badge/Release%20Health-Run%20check-2ea44f?logo=powershell&logoColor=white" alt="Release Health"/></a>
</p>

---
<!-- PROGESI:HERO:END -->

<!-- PROGESI:GPR:START -->
## Using GitHub Packages (GPR)

### Option A — `nuget.config`

Create a `nuget.config` next to your solution and set **GPR_PAT** env var with `read:packages` scope.

```xml
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="github" value="https://nuget.pkg.github.com/GianlucaProgesi/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <github>
      <add key="Username" value="GianlucaProgesi" />
      <add key="ClearTextPassword" value="%GPR_PAT%" />
    </github>
  </packageSourceCredentials>
</configuration>
```

Windows (PowerShell):
```powershell
$Env:GPR_PAT = "<your PAT with read:packages>"
dotnet nuget remove source github 2>$null; dotnet nuget add source "https://nuget.pkg.github.com/GianlucaProgesi/index.json" --name "github" --username "GianlucaProgesi" --password "$Env:GPR_PAT" --store-password-in-clear-text
```

macOS/Linux (bash):
```bash
export GPR_PAT="<your PAT with read:packages>"
dotnet nuget remove source github >/dev/null 2>&1; dotnet nuget add source "https://nuget.pkg.github.com/GianlucaProgesi/index.json" --name "github" --username "GianlucaProgesi" --password "$GPR_PAT" --store-password-in-clear-text
```
<!-- PROGESI:GPR:END -->

<!-- PROGESI:PACKAGES:START -->
## Packages

| Package | NuGet | Install |
|---|---|---|
| ProgesiCore | [nuget.org](https://www.nuget.org/packages/ProgesiCore) | `dotnet add package ProgesiCore` |
| ProgesiRepositories.InMemory | [nuget.org](https://www.nuget.org/packages/ProgesiRepositories.InMemory) | `dotnet add package ProgesiRepositories.InMemory` |
| ProgesiRepositories.Rhino | [nuget.org](https://www.nuget.org/packages/ProgesiRepositories.Rhino) | `dotnet add package ProgesiRepositories.Rhino` |
| ProgesiRepositories.Sqlite | [nuget.org](https://www.nuget.org/packages/ProgesiRepositories.Sqlite) | `dotnet add package ProgesiRepositories.Sqlite` |
<!-- PROGESI:PACKAGES:END -->

<!-- PROGESI:BADGES:START -->
[![Release Health](https://img.shields.io/badge/Release%20Health-Run%20check-2ea44f?logo=powershell&logoColor=white)](tools/Release-HealthCheck.ps1)
<!-- PROGESI:BADGES:END -->

<!-- PROGESI:OVERVIEW:START -->

## ℹ️ Overview

**Progesi** – a modular toolchain for bridge and structural engineering:
- 🧩 **Grasshopper/Rhino components** for variables, metadata, and repositories
- 📦 Modular **NuGet packages** with SourceLink and built-in docs
- 🚀 Automated **CI/CD pipeline** (NuGet.org + GitHub Packages)
- 📝 Auto-generated **CHANGELOG** and **README** via PowerShell scripts
- ✅ **Health check** and maintenance checklist for reliable releases

---

**Progesi** è una toolchain modulare per l’ingegneria dei ponti e delle strutture complesse:
- 🧩 Componenti **Grasshopper/Rhino** per variabili, metadata e repository
- 📦 Pacchetti **NuGet** modulari con SourceLink e documentazione integrata
- 🚀 Pipeline **CI/CD** automatizzata (NuGet.org + GitHub Packages)
- 📝 **CHANGELOG** e **README** generati automaticamente via script PowerShell
- ✅ **Health check** e checklist di manutenzione per rilasci affidabili

<!-- PROGESI:OVERVIEW:END -->


![Coverage](docs/coverage/badge_linecoverage.svg)

[![CI](https://github.com/GianlucaProgesi/Progesi/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/GianlucaProgesi/Progesi/actions/workflows/ci.yml)

(incolla QUI tutto il contenuto markdown riportato sopra)

<!-- PROGESI:QUICKSTART:START -->
## Quick start

```csharp
// using ProgesiCore;
// var v = new ProgesiVariable("Span", 35.0);
```
<!-- PROGESI:QUICKSTART:END -->

<!-- PROGESI:RELMAINT:START -->

## 🔧 Release & Maintenance

- 📖 **Release Flow:** vedi [docs/RELEASE-FLOW.md](docs/RELEASE-FLOW.md)  
- 🛠️ **Maintenance Checklist:** vedi [docs/RELEASE-MAINTENANCE.md](docs/RELEASE-MAINTENANCE.md)  
- 🚦 **Health Check (prima di un rilascio):
  ```powershell
  pwsh -File ./tools/Release-HealthCheck.ps1
  ```
- 🚀 **One-liner di rilascio:**
  ```powershell
  # simulazione
  pwsh -File ./tools/End-to-End-Release.ps1 -DryRun

  # rilascio reale
  pwsh -File ./tools/End-to-End-Release.ps1
  ```

<!-- PROGESI:RELMAINT:END -->

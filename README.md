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
[![Build](https://github.com/GianlucaProgesi/Progesi/actions/workflows/release.yml/badge.svg)](https://github.com/GianlucaProgesi/Progesi/actions/workflows/release.yml)

[![NuGet ProgesiCore](https://img.shields.io/nuget/v/ProgesiCore.svg)](https://www.nuget.org/packages/ProgesiCore)
[![NuGet ProgesiRepositories.InMemory](https://img.shields.io/nuget/v/ProgesiRepositories.InMemory.svg)](https://www.nuget.org/packages/ProgesiRepositories.InMemory)
[![NuGet ProgesiRepositories.Rhino](https://img.shields.io/nuget/v/ProgesiRepositories.Rhino.svg)](https://www.nuget.org/packages/ProgesiRepositories.Rhino)
[![NuGet ProgesiRepositories.Sqlite](https://img.shields.io/nuget/v/ProgesiRepositories.Sqlite.svg)](https://www.nuget.org/packages/ProgesiRepositories.Sqlite)

![Downloads ProgesiCore](https://img.shields.io/nuget/dt/ProgesiCore)
![Downloads ProgesiRepositories.InMemory](https://img.shields.io/nuget/dt/ProgesiRepositories.InMemory)
![Downloads ProgesiRepositories.Rhino](https://img.shields.io/nuget/dt/ProgesiRepositories.Rhino)
![Downloads ProgesiRepositories.Sqlite](https://img.shields.io/nuget/dt/ProgesiRepositories.Sqlite)
<!-- PROGESI:BADGES:END -->

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



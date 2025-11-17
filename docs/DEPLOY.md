# Deploy del plugin Progesi (GH "pure SQLite")

Queste istruzioni permettono di distribuire il plugin di Grasshopper (senza dipendenze EF) e, opzionalmente, il tool EF da riga di comando.

## Prerequisiti

- Windows 10/11
- Rhino 7/8 installato
- .NET Framework 4.8 Dev Pack
- Git e .NET SDK 9+ se vuoi compilare e fare il push

Percorsi rilevanti:
- Radice repo: `C:\...\Progesi\`
- Output GH: `src\ProgesiGrasshopperAssembly\bin\Release\net48\`
- Libreria utente di Grasshopper: `%APPDATA%\Grasshopper\Libraries`

## 1) Compilazione

Dalla root del repository:

```powershell
dotnet clean
dotnet build -c Release
dotnet test  -c Release --no-build

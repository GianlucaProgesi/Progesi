# ProgesiRepositories.Rhino  [![NuGet ProgesiRepositories.Rhino](https://img.shields.io/nuget/v/ProgesiRepositories.Rhino.svg)](https://www.nuget.org/packages/ProgesiRepositories.Rhino) ![Downloads ProgesiRepositories.Rhino](https://img.shields.io/nuget/dt/ProgesiRepositories.Rhino)   <!-- PROGESI:BODY:START --> Rhino-based repository (integration with Rhino/Grasshopper data).  ## Install `dotnet add package ProgesiRepositories.Rhino`  ## Quick note - Requires Rhino environment for runtime integration. - Use together with ProgesiCore.  ## Example (conceptual) ```csharp using ProgesiCore; using ProgesiRepositories.Rhino;  // var repo = new RhinoVariablesRepository(doc); // repo.Save(new ProgesiVariable("Span", 35.0)); ```  ## Links - NuGet: https://www.nuget.org/packages/ProgesiRepositories.Rhino <!-- PROGESI:BODY:END -->

# ProgesiRepositories.Rhino

Implementazione di `IVariableRepository` che salva le `ProgesiVariable` dentro il `RhinoDoc` usando `RhinoDoc.Strings`.
- Persistenza nel file `.3dm`.
- Indice di tutti gli Id mantenuto in `ProgesiVariables:Index` (JSON array).
- Payload serializzato con Newtonsoft.Json.

## Setup
1. Aggiungi **Reference** a `RhinoCommon.dll`:
   - Esempio percorso: `C:\Program Files\Rhino 8\System\RhinoCommon.dll` (o Rhino 7).
2. Aggiungi il progetto alla solution e referenzialo dove serve (es. nel componente GH).
3. Costruisci l'istanza
   ```csharp
   var repo = new ProgesiRepositories.Rhino.RhinoVariableRepository();
   ```

## Note
- Al momento serializziamo i tipi comuni (int, double, string, bool). Per geometrie, in futuro: WKT/JSON o `Brep.Encode()` etc.


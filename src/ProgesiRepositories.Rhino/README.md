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

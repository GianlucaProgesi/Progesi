using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using ProgesiCore;
using ProgesiGrasshopperAssembly.Infrastructure;

namespace ProgesiGrasshopperAssembly.Components
{
  public sealed class ProgesiVariableInComponent : GH_Component
  {
    public ProgesiVariableInComponent()
      : base("ProgesiVariableIn", "VarIn",
             "Create / Update / Delete a ProgesiVariable (Core repo, generic Value).",
             "Progesi", "Variables")
    { }

    public override Guid ComponentGuid => new Guid("E7F6D4B1-0F7E-4A7B-9F6D-5C0C5D8C1A10");

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Run: True|False", GH_ParamAccess.item, false);
      p.AddTextParameter("Action", "Act", "Action: Create|Update|Delete (default Create)", GH_ParamAccess.item, "Create");
      p.AddIntegerParameter("Id", "Id", "Existing variable Id (int). Required for Update/Delete. Leave 0/empty for Create.", GH_ParamAccess.item, 0);
      p.AddTextParameter("Name", "Name", "Variable Name (required for Create).", GH_ParamAccess.item, "New Progesi Variable");
      p.AddGenericParameter("Value", "Value", "Generic: Numerical|String|Rhino/Grasshopper object. Cannot be null.", GH_ParamAccess.item);
      p.AddNumberParameter("Factor", "Factor", "Scale factor (applied only if Value is numeric).", GH_ParamAccess.item, 1.0);
      p.AddIntegerParameter("DependsFromId", "DepFromId", "List of variable ids this one depends on. Leave empty if independent.", GH_ParamAccess.list);
      p.AddIntegerParameter("MetadataIds", "Metaids", "List of ProgesiMetadata ids associated (first non-zero is used).", GH_ParamAccess.list);

      // opzionali
      Params.Input[2].Optional = true; // Id
      Params.Input[6].Optional = true; // DepFromId
      Params.Input[7].Optional = true; // Metaids
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddIntegerParameter("Id", "Id", "0 if deleted; otherwise Id of the variable.", GH_ParamAccess.item);
      p.AddTextParameter("HashCode", "Hash", "Empty if deleted; otherwise a rich hash of the variable.", GH_ParamAccess.item);
      p.AddTextParameter("Name", "Name", "Empty if deleted; otherwise the name of the variable.", GH_ParamAccess.item);
      p.AddTextParameter("Info", "Info", "Operation results.", GH_ParamAccess.item);
    }

    // === Helpers ===
    static string NormalizeAction(string s)
    {
      if (string.IsNullOrWhiteSpace(s)) return "Create";
      if (s.Equals("create", StringComparison.OrdinalIgnoreCase)) return "Create";
      if (s.Equals("update", StringComparison.OrdinalIgnoreCase)) return "Update";
      if (s.Equals("delete", StringComparison.OrdinalIgnoreCase)) return "Delete";
      return "Create";
    }

    static object Unwrap(object maybe)
    {
      if (maybe is IGH_Goo goo)
      {
        try { return goo.ScriptVariable(); } catch { }
      }
      return maybe;
    }

    static string Sha256Hex(string s)
    {
      using var sha = SHA256.Create();
      byte[] bytes = Encoding.UTF8.GetBytes(s ?? string.Empty);
      byte[] hash = sha.ComputeHash(bytes);
      var sb = new StringBuilder(hash.Length * 2);
      foreach (var b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
      return sb.ToString();
    }

    static string BuildRichSignature(string name, object? value, IEnumerable<int> depends, int? metadataId)
    {
      // Value canonico dal Core (numeri formattati invarianti, ecc.)
      string val = ProgesiHash.CanonicalValue(value);
      string deps = string.Join(",", (depends ?? Array.Empty<int>()).OrderBy(x => x));
      string mid = metadataId.HasValue ? metadataId.Value.ToString(CultureInfo.InvariantCulture) : "";
      string payload = $"{name}|{val}|{deps}|{mid}";
      return Sha256Hex(payload); // firma leggibile/consistente
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
      // --- Read inputs ---
      bool run = false;
      string action = "Create";
      int id = 0;
      string name = string.Empty;
      object valueRaw = null!;
      double factor = 1.0;
      var dependsFrom = new List<int>();
      var metadataIds = new List<int>();

      DA.GetData(0, ref run);
      DA.GetData(1, ref action);
      DA.GetData(2, ref id);          // opzionale
      DA.GetData(3, ref name);
      DA.GetData(4, ref valueRaw);
      DA.GetData(5, ref factor);
      DA.GetDataList(6, dependsFrom); // opzionale (vuoto = ok)
      DA.GetDataList(7, metadataIds); // opzionale (vuoto = ok)

      void Emit(int idOut, string hash, string nm, string info)
      {
        DA.SetData(0, idOut);
        DA.SetData(1, hash ?? string.Empty);
        DA.SetData(2, nm ?? string.Empty);
        DA.SetData(3, info ?? string.Empty);
      }

      if (!run) { Emit(0, string.Empty, string.Empty, "Idle"); return; }

      var act = NormalizeAction(action);

      // Validazioni minime
      if (act == "Update" || act == "Delete")
      {
        if (id <= 0)
        {
          const string msg = "Id is required for Update/Delete.";
          AddRuntimeMessage(GH_RuntimeMessageLevel.Error, msg);
          Emit(0, string.Empty, string.Empty, msg);
          return;
        }
      }

      if (act != "Delete")
      {
        if (string.IsNullOrWhiteSpace(name))
        {
          const string msg = "Name is required for Create/Update.";
          AddRuntimeMessage(GH_RuntimeMessageLevel.Error, msg);
          Emit(0, string.Empty, string.Empty, msg);
          return;
        }
        if (valueRaw is null)
        {
          const string msg = "Value cannot be null.";
          AddRuntimeMessage(GH_RuntimeMessageLevel.Error, msg);
          Emit(0, string.Empty, string.Empty, msg);
          return;
        }
      }

      try
      {
        if (act == "Delete")
        {
          // In P1 non abbiamo Delete nel repo: emettiamo risposta coerente.
          Emit(0, string.Empty, string.Empty, "The variable has been eliminated (logical).");
          return;
        }

        // Preparazione dati
        var unwrapped = Unwrap(valueRaw);
        object? valueToSave = unwrapped;
        if (unwrapped is double d) valueToSave = d * factor;
        else if (unwrapped is float f) valueToSave = f * (float)factor;
        else if (unwrapped is int i) valueToSave = (int)Math.Round(i * factor, MidpointRounding.AwayFromZero);
        else if (unwrapped is long l) valueToSave = (long)Math.Round(l * factor, MidpointRounding.AwayFromZero);

        var depends = dependsFrom?.ToArray() ?? Array.Empty<int>();
        int? metadataId = null;
        if (metadataIds != null)
        {
          var first = metadataIds.FirstOrDefault(x => x > 0);
          if (first > 0) metadataId = first;
        }

        // ==== CREATE: riuso id se esiste, altrimenti id progressivo ====
        if (act == "Create")
        {
          // firma input
          string inSig = BuildRichSignature(name.Trim(), valueToSave, depends, metadataId);

          // cerca nel repo
          var all = ServiceHubCore.Variables.GetAllAsync().GetAwaiter().GetResult();
          var existing = all.FirstOrDefault(v =>
          {
            string sig = BuildRichSignature(v.Name, v.Value, v.DependsFrom, v.MetadataId);
            return string.Equals(sig, inSig, StringComparison.Ordinal);
          });

          ProgesiVariable saved;
          bool reused;

          if (existing != null)
          {
            // riuso
            saved = existing;
            reused = true;
          }
          else
          {
            // nuovo id progressivo
            int nextId = all.Count == 0 ? 1 : all.Max(v => v.Id) + 1;
            var toSave = new ProgesiVariable(nextId, name.Trim(), valueToSave, depends, metadataId);
            saved = ServiceHubCore.Variables.SaveAsync(toSave).GetAwaiter().GetResult();
            reused = false;
          }

          string hashStr = BuildRichSignature(saved.Name, saved.Value, saved.DependsFrom, saved.MetadataId);
          string typeName = (saved.Value?.GetType()?.Name ?? "null");
          string info;

          if (reused)
            info = $"The ProgesiVariable '{saved.Name}', type '{typeName}' already existed and has been reused with id '{saved.Id}'.";
          else if (saved.Value is string s)
            info = $"The ProgesiVariable '{saved.Name}', type 'string' has been created with id '{saved.Id}' and value '{s}'.";
          else if (saved.Value is double || saved.Value is float || saved.Value is int || saved.Value is long)
            info = $"The ProgesiVariable '{saved.Name}', type '{typeName}' has been created with id '{saved.Id}' and value {saved.Value}.";
          else
            info = $"The ProgesiVariable '{saved.Name}', type '{typeName}' has been created with id '{saved.Id}'.";

          Emit(saved.Id, hashStr, saved.Name, info);
          return;
        }

        // ==== UPDATE ====
        {
          var input = new ProgesiVariable(id, name.Trim(), valueToSave, depends, metadataId);
          var saved = ServiceHubCore.Variables.SaveAsync(input).GetAwaiter().GetResult();

          string hashStr = BuildRichSignature(saved.Name, saved.Value, saved.DependsFrom, saved.MetadataId);
          string info = $"The ProgesiVariable '{saved.Name}' with id '{saved.Id}' has been successfully updated.";
          Emit(saved.Id, hashStr, saved.Name, info);
        }
      }
      catch (Exception ex)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
        Emit(0, string.Empty, string.Empty, "Error: " + ex.Message);
      }
    }
  }
}

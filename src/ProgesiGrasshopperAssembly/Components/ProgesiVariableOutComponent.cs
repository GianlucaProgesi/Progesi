using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Grasshopper.Kernel;
using ProgesiCore;
using ProgesiGrasshopperAssembly.Infrastructure;

namespace ProgesiGrasshopperAssembly.Components
{
  public sealed class ProgesiVariableOutComponent : GH_Component
  {
    public ProgesiVariableOutComponent()
      : base("ProgesiVariableOut", "VarOut",
             "Fetch a ProgesiVariable (by Hash, Id or Name) and expose its fields.",
             "Progesi", "Variables")
    { }

    public override Guid ComponentGuid => new Guid("9C7C2A95-1F44-4D04-87E0-0CF7F7F83CA2");

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      // A: Run; H: Hash (priorità 1); Id (priorità 2); Name (priorità 3, case-insensitive)
      p.AddBooleanParameter("Run", "Run", "Run: True|False", GH_ParamAccess.item, false);
      p.AddTextParameter("Hash", "Hash", "Rich hash (Name|Value|DependsFrom|MetadataId). Highest priority selector.", GH_ParamAccess.item, string.Empty);
      p.AddIntegerParameter("Id", "Id", "Variable Id (int). Used if Hash is empty.", GH_ParamAccess.item, 0);
      p.AddTextParameter("Name", "Name", "Variable Name (case-insensitive). Used if Hash and Id are empty.", GH_ParamAccess.item, string.Empty);

      Params.Input[1].Optional = true; // Hash
      Params.Input[2].Optional = true; // Id
      Params.Input[3].Optional = true; // Name
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      // Id, Hash “ricco”, Name, Value (generic), DepFromId (list), Metaids (list), Info
      p.AddIntegerParameter("Id", "Id", "Id of the variable, or 0 if not found.", GH_ParamAccess.item);
      p.AddTextParameter("HashCode", "Hash", "Rich hash of the variable (Name|Value|DependsFrom|MetadataId). Empty if not found.", GH_ParamAccess.item);
      p.AddTextParameter("Name", "Name", "Variable name. Empty if not found.", GH_ParamAccess.item);
      p.AddGenericParameter("Value", "Value", "Variable value as GH generic. Null/empty if not found.", GH_ParamAccess.item);
      p.AddIntegerParameter("DepFromId", "DepFromId", "DependsFrom ids (may be empty).", GH_ParamAccess.list);
      p.AddIntegerParameter("MetadataIds", "Metaids", "Metadata ids (0 or 1 element taken from MetadataId).", GH_ParamAccess.list);
      p.AddTextParameter("Info", "Info", "Operation results.", GH_ParamAccess.item);
    }

    // === Helpers coerenti con VarIn ===
    static string Sha256Hex(string s)
    {
      using var sha = SHA256.Create();
      byte[] bytes = Encoding.UTF8.GetBytes(s ?? string.Empty);
      byte[] hash = sha.ComputeHash(bytes);
      var sb = new StringBuilder(hash.Length * 2);
      foreach (var b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
      return sb.ToString();
    }

    static string BuildRichSignature(ProgesiVariable v)
    {
      var val = ProgesiHash.CanonicalValue(v.Value);
      var deps = string.Join(",", (v.DependsFrom ?? Array.Empty<int>()).OrderBy(x => x));
      var mid = v.MetadataId.HasValue ? v.MetadataId.Value.ToString(CultureInfo.InvariantCulture) : "";
      string payload = $"{v.Name}|{val}|{deps}|{mid}";
      return Sha256Hex(payload);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
      bool run = false;
      string hashIn = string.Empty;
      int id = 0;
      string name = string.Empty;

      DA.GetData(0, ref run);
      DA.GetData(1, ref hashIn);
      DA.GetData(2, ref id);
      DA.GetData(3, ref name);

      void EmitNotFound(string info)
      {
        DA.SetData(0, 0);               // Id
        DA.SetData(1, string.Empty);    // Hash
        DA.SetData(2, string.Empty);    // Name
        DA.SetData(3, null);            // Value
        DA.SetDataList(4, Array.Empty<int>());   // DepFromId
        DA.SetDataList(5, Array.Empty<int>());   // Metaids
        DA.SetData(6, info ?? "Not found.");
      }

      if (!run)
      {
        EmitNotFound("Idle");
        return;
      }

      try
      {
        ProgesiVariable? v = null;

        // PRIORITÀ 1: HASH (case-insensitive sui caratteri esadecimali)
        if (!string.IsNullOrWhiteSpace(hashIn))
        {
          string want = hashIn.Trim();
          var all = ServiceHubCore.Variables.GetAllAsync().GetAwaiter().GetResult();
          v = all.FirstOrDefault(x => string.Equals(BuildRichSignature(x), want, StringComparison.OrdinalIgnoreCase));
        }

        // PRIORITÀ 2: ID
        if (v == null && id > 0)
        {
          v = ServiceHubCore.Variables.GetByIdAsync(id).GetAwaiter().GetResult();
        }

        // PRIORITÀ 3: NAME (case-insensitive)
        if (v == null && !string.IsNullOrWhiteSpace(name))
        {
          var all = ServiceHubCore.Variables.GetAllAsync().GetAwaiter().GetResult();
          v = all.FirstOrDefault(x => string.Equals(x.Name ?? string.Empty, name.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (v == null)
        {
          EmitNotFound("Not found.");
          return;
        }

        // Build outputs
        string hash = BuildRichSignature(v);
        var deps = (v.DependsFrom ?? Array.Empty<int>()).ToArray();
        var metas = v.MetadataId.HasValue ? new[] { v.MetadataId.Value } : Array.Empty<int>();

        DA.SetData(0, v.Id);
        DA.SetData(1, hash);
        DA.SetData(2, v.Name ?? string.Empty);
        DA.SetData(3, v.Value);
        DA.SetDataList(4, deps);
        DA.SetDataList(5, metas);
        DA.SetData(6, "OK");
      }
      catch (Exception ex)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
        EmitNotFound("Error: " + ex.Message);
      }
    }
  }
}

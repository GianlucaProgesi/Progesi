using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Progesi.Grasshopper.Browsers.Internal;

namespace Progesi.Grasshopper.Browsers.Components
{
  public sealed class ProgesiMetadataBrowserComponent : GH_Component
  {
    public ProgesiMetadataBrowserComponent()
      : base("Progesi Metadata Browser", "MetBrowser", "Read-only view of metadata table (filterable).", "Progesi", "Browsers")
    { }

    public override Guid ComponentGuid => new Guid("A1C8D6E9-03C2-4D27-85D2-7B5A1D5B4B01");
    protected override System.Drawing.Bitmap Icon => null;
    public override GH_Exposure Exposure => GH_Exposure.secondary;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Trigger the query execution.", GH_ParamAccess.item, false);
      p.AddTextParameter("DbPath", "Db", "Path to the SQLite database (default: data\\progesi.db).", GH_ParamAccess.item, @"data\progesi.db");
      p.AddTextParameter("FilterHash", "Hash", "Filter by Hash (contains).", GH_ParamAccess.item, "");
      p.AddTextParameter("FilterBy", "By", "Filter by author/created-by (if column exists).", GH_ParamAccess.item, "");
      p.AddTextParameter("FilterRef", "Ref", "Filter by Ref (contains, if column exists).", GH_ParamAccess.item, "");
      p.AddIntegerParameter("Limit", "N", "Max rows (1..1000).", GH_ParamAccess.item, 200);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddTextParameter("Headers", "H", "Headers of the table.", GH_ParamAccess.list);
      p.AddTextParameter("Rows", "R", "Rows as flattened strings (use RowsTree for structured).", GH_ParamAccess.list);
      p.AddTextParameter("Info", "Info", "Execution info or errors.", GH_ParamAccess.item);
      p.AddIntegerParameter("Count", "#", "Number of rows returned.", GH_ParamAccess.item);
      p.AddTextParameter("RowsTree", "Tree", "Rows as a tree: each branch is a row with cells.", GH_ParamAccess.tree);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
      bool run = false; string db = null; string fHash = null; string fBy = null; string fRef = null; int limit = 200;
      da.GetData(0, ref run);
      da.GetData(1, ref db);
      da.GetData(2, ref fHash);
      da.GetData(3, ref fBy);
      da.GetData(4, ref fRef);
      da.GetData(5, ref limit);

      if (!run)
      {
        da.SetDataList(0, new List<string>());
        da.SetDataList(1, new List<string>());
        da.SetData(2, "Idle");
        da.SetData(3, 0);
        da.SetDataTree(4, new GH_Structure<GH_String>());
        return;
      }

      try
      {
        using (var b = new SqliteBrowser(db))
        {
          var (headers, rows) = b.BrowseMetadata(fHash ?? "", fBy ?? "", fRef ?? "", limit);
          da.SetDataList(0, headers);

          var flat = new List<string>(rows.Count);
          var tree = new GH_Structure<GH_String>();

          for (int i = 0; i < rows.Count; i++)
          {
            var row = rows[i];
            var path = new GH_Path(i);
            flat.Add(string.Join(" | ", row));
            foreach (var cell in row)
              tree.Append(new GH_String(cell), path);
          }

          da.SetDataList(1, flat);
          da.SetData(2, $"OK ({rows.Count} rows)");
          da.SetData(3, rows.Count);
          da.SetDataTree(4, tree);
        }
      }
      catch (Exception ex)
      {
        da.SetDataList(0, new List<string>());
        da.SetDataList(1, new List<string>());
        da.SetData(2, $"Error: {ex.Message}");
        da.SetData(3, 0);
        da.SetDataTree(4, new GH_Structure<GH_String>());
      }
    }
  }
}

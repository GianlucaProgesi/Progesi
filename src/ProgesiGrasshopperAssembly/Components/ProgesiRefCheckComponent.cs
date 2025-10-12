#nullable disable
using System;
using System.Drawing;
using Grasshopper.Kernel;
using ProgesiGrasshopperAssembly.Infrastructure;

namespace Progesi.GrasshopperAssembly.Components
{
  /// <summary>Valida e normalizza un Ref (file/URL/data-url).</summary>
  public sealed class ProgesiRefCheckComponent : GH_Component
  {
    public ProgesiRefCheckComponent()
      : base("Progesi Ref Check", "RefCheck",
             "Valida e normalizza un Ref (file/URL/data-url).",
             "Progesi", "Utils")
    { }

    public override Guid ComponentGuid => new Guid("C9D73D73-FA39-4C9B-B9A1-73C570B2AFDE");
    protected override Bitmap Icon => ProgesiIcons.RefCheck;
    public override GH_Exposure Exposure => GH_Exposure.secondary;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Esegui (False = nessuna operazione).", GH_ParamAccess.item, false);
      p.AddTextParameter("Ref", "Ref", "Riferimento (file path, URL http/https, data-url).", GH_ParamAccess.item, "");
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddTextParameter("Normalized", "Norm", "Ref normalizzato (path assoluto o URL).", GH_ParamAccess.item);
      p.AddTextParameter("Info", "Info", "Esito: OK / motivo errore (File non trovato, URL non valido, ...).", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
      bool run = false; string inputRef = "";
      da.GetData(0, ref run);
      da.GetData(1, ref inputRef);

      if (!run) { da.SetData(0, ""); da.SetData(1, "Idle"); return; }

      string norm, why;
      if (SnipHelpers.TryNormalizeRef(inputRef ?? "", out norm, out why))
      {
        da.SetData(0, norm);
        da.SetData(1, "OK");
      }
      else
      {
        da.SetData(0, "");
        da.SetData(1, "Invalid Ref: " + (string.IsNullOrWhiteSpace(why) ? "non valido" : why));
      }
    }
  }
}

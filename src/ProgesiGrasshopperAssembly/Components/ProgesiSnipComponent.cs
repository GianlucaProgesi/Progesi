#nullable enable
using System;
using Grasshopper.Kernel;
using ProgesiGrasshopperAssembly.Infrastructure;

namespace ProgesiGrasshopperAssembly.Components
{
  /// <summary>
  /// Converte immagini (bitmap, path file, url, byte[]) in Snip string compatibili:
  /// snip:{index}:{mime}:caption={...}:origin={path|url|inline}
  /// </summary>
  public sealed class ProgesiSnipComponent : GH_Component
  {
    public ProgesiSnipComponent()
      : base("ProgesiSnip", "Snip",
             "Crea Snip compatibili da bitmap, file path, url o byte[].",
             "Progesi", "Metadata")
    { }

    public override Guid ComponentGuid => new Guid("6A4F8D3E-35B8-4F47-9B0A-2F3A5B7B3B10");
    protected override System.Drawing.Bitmap Icon => ProgesiIcons.Transparent1px;

    private int _inRun, _inInput, _inCaption, _inIndex;
    private int _outSnip, _outInfo;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      _inRun = p.AddBooleanParameter("Run", "Run", "Esegui (default: True).", GH_ParamAccess.item, true);
      _inInput = p.AddGenericParameter("Input", "Input", "Bitmap, path file, url o byte[].", GH_ParamAccess.item);
      _inCaption = p.AddTextParameter("Caption", "Cap", "Didascalia (opzionale).", GH_ParamAccess.item, string.Empty);
      _inIndex = p.AddIntegerParameter("Index", "Idx", "Indice snip (default 1).", GH_ParamAccess.item, 1);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      _outSnip = p.AddTextParameter("Snip", "Snip", "Snip generato.", GH_ParamAccess.item);
      _outInfo = p.AddTextParameter("Info", "Info", "Stato operazione.", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
      bool run = true;
      DA.GetData(_inRun, ref run);
      if (!run) { DA.SetData(_outSnip, string.Empty); DA.SetData(_outInfo, "Idle"); return; }

      object? input = null; string? caption = string.Empty; int index = 1;

      if (!DA.GetData(_inInput, ref input) || input is null)
      { DA.SetData(_outSnip, string.Empty); DA.SetData(_outInfo, "Nessun input"); return; }

      DA.GetData(_inCaption, ref caption);
      DA.GetData(_inIndex, ref index);

      string snip; string info;
      if (SnipHelpers.TryMake(input, caption, index, out snip, out info))
      { DA.SetData(_outSnip, snip); DA.SetData(_outInfo, info); }
      else
      { DA.SetData(_outSnip, string.Empty); DA.SetData(_outInfo, info); }
    }
  }
}

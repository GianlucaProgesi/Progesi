#nullable disable
using System;
using System.Reflection;
using System.Drawing;
using System.IO;
using System.Windows.Forms; // Clipboard
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using ProgesiGrasshopperAssembly.Infrastructure;

namespace ProgesiGrasshopperAssembly.Components
{
  public sealed class ProgesiSnipComponent : GH_Component
  {
    public ProgesiSnipComponent()
      : base("ProgesiSnip", "Snip",
             "Normalizza un input in formato Snip.\nAccetta: snip:*/data-url/base64/path/url/Bitmap.\n" +
             "Se 'Clip'=TRUE e l'input è vuoto, usa l'immagine dalla clipboard (se presente).",
             "Progesi", "Utils")
    { }

    public override Guid ComponentGuid => new Guid("D5B6D6C9-23A8-4BA1-8A2D-7C893F2DA3B9");
    protected override System.Drawing.Bitmap Icon => ProgesiIcons.Snip;

    // IN: Run, Input (obj), Caption, Clip
    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Esegui (default TRUE).", GH_ParamAccess.item, true);
      p.AddGenericParameter("Input", "Inp", "String/Path/URL/DataURL/Base64/Bitmap (drag&drop su parametro) o GH_FilePath.", GH_ParamAccess.item);
      p.AddTextParameter("Caption", "Cap", "Didascalia opzionale.", GH_ParamAccess.item, "");
      p.AddBooleanParameter("Clip", "Clip", "Se TRUE e Inp è vuoto, usa l'immagine dalla Clipboard (se presente).", GH_ParamAccess.item, false);
      Params.Input[1].Optional = true; // Inp opzionale
    }

    // OUT: Snip, Info
    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddTextParameter("Snip", "Snip", "Snip normalizzato.", GH_ParamAccess.item);
      p.AddTextParameter("Info", "Info", "Esito/diagnostica.", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
      bool run = true; object input = null; string cap = ""; bool useClip = false;
      DA.GetData(0, ref run);
      DA.GetData(1, ref input);
      DA.GetData(2, ref cap);
      DA.GetData(3, ref useClip);

      if (!run) { DA.SetData(0, ""); DA.SetData(1, "Idle"); return; }

      // Clipboard fallback
      if ((input == null || (input is string s0 && string.IsNullOrWhiteSpace(s0))) && useClip)
      {
        try
        {
          if (Clipboard.ContainsImage())
          {
            var img = Clipboard.GetImage();
            if (img != null) input = new Bitmap(img);
          }
        }
        catch { /* silent */ }
      }

      // Normalizza input: gestiamo IGH_Goo → string/Bitmap/Uri/FileInfo
      var norm = NormalizeGhInput(input);

      if (norm == null)
      {
        DA.SetData(0, ""); DA.SetData(1, "Input snip vuoto"); return;
      }

      const int index = 1;
      if (SnipHelpers.TryMake(norm, cap ?? "", index, out var outSnip, out var outInfo))
      {
        if (string.IsNullOrWhiteSpace(outInfo)) outInfo = "OK";
        DA.SetData(0, outSnip ?? "");
        DA.SetData(1, outInfo);
      }
      else
      {
        DA.SetData(0, "");
        DA.SetData(1, string.IsNullOrWhiteSpace(outInfo) ? "Formato Snip non riconosciuto" : outInfo);
      }
    }

    private static object NormalizeGhInput(object input)
    {
      if (input == null) return null;

      // già Bitmap/Uri/string
      if (input is Bitmap || input is Uri || input is string) return input;

      // GH types
      if (input is IGH_Goo goo)
      {
        // string (GH_FilePath, GH_String, ecc.)
        string s;
        if (goo.CastTo(out s) && !string.IsNullOrWhiteSpace(s)) return s;

        // Bitmap
        Bitmap bmp;
        if (goo.CastTo(out bmp) && bmp != null) return bmp;

        // Uri
        Uri u;
        if (goo.CastTo(out u) && u != null) return u;

        // FileInfo
        FileInfo fi;
        if (goo.CastTo(out fi) && fi != null) return fi.FullName;
      }

      // fallback: ToString
      return input.ToString();
    }
  }
}

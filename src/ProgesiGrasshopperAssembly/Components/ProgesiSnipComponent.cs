#nullable disable
using System;
using System.Reflection;
using System.Drawing;
using Grasshopper.Kernel;
using ProgesiGrasshopperAssembly.Infrastructure;

namespace ProgesiGrasshopperAssembly.Components
{
  public sealed class ProgesiSnipComponent : GH_Component
  {
    public ProgesiSnipComponent()
      : base("ProgesiSnip", "Snip",
             "Normalizza un input in formato Snip.\nAccetta: snip:*/data-url/base64/path/url.",
             "Progesi", "Utils")
    { }

    public override Guid ComponentGuid => new Guid("B7A2DF35-5F8E-43E1-9FBB-2EBF572A4E6B");
    protected override Bitmap Icon => ProgesiIcons.Snip;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Esegui (default TRUE).", GH_ParamAccess.item, true);
      p.AddGenericParameter("Input", "Input", "snip:*/data-url/base64/path/url.", GH_ParamAccess.item);
      p.AddTextParameter("Cap", "Cap", "Caption (verrà sanitizzato a 120 char).", GH_ParamAccess.item, "");
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddTextParameter("Snip", "Snip", "Snip normalizzato (snip:index:mime:caption=...)", GH_ParamAccess.item);
      p.AddTextParameter("Info", "Info", "Esito/diagnostica (OK / Invalid ...).", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
      bool run = true; object input = null; string cap = "";
      DA.GetData(0, ref run);
      DA.GetData(1, ref input);
      DA.GetData(2, ref cap);

      if (!run) { DA.SetData(0, ""); DA.SetData(1, "Idle"); return; }

      string s = ExtractString(input);
      if (string.IsNullOrWhiteSpace(s)) { DA.SetData(0, ""); DA.SetData(1, "Input snip vuoto"); return; }

      const int index = 1;
      if (SnipHelpers.TryMake(s, cap ?? "", index, out var outSnip, out var outInfo))
      {
        if (string.IsNullOrWhiteSpace(outInfo)) outInfo = "OK";
        DA.SetData(0, outSnip);
        DA.SetData(1, outInfo);
      }
      else
      {
        DA.SetData(0, outSnip ?? "");
        DA.SetData(1, string.IsNullOrWhiteSpace(outInfo) ? "Invalid Snip" : outInfo);
      }
    }

    private static string ExtractString(object input)
    {
      if (input == null) return string.Empty;
      if (input is string ss) return ss;

      var t = input.GetType();
      var iface = t.GetInterface("Grasshopper.Kernel.Types.IGH_Goo");
      if (iface != null)
      {
        try
        {
          var m = t.GetMethod("CastTo", BindingFlags.Public | BindingFlags.Instance);
          if (m != null && m.IsGenericMethodDefinition)
          {
            var castToString = m.MakeGenericMethod(typeof(string));
            var args = new object[] { null };
            var ok = (bool)castToString.Invoke(input, args);
            if (ok && args[0] is string gooStr && !string.IsNullOrEmpty(gooStr)) return gooStr;
          }
        }
        catch { }
        var vp = t.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
        if (vp != null)
        {
          var v = vp.GetValue(input, null);
          if (v != null) return v.ToString();
        }
      }

      var pVal = t.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
      if (pVal != null)
      {
        var v = pVal.GetValue(input, null);
        if (v != null) return v.ToString();
      }
      return input.ToString() ?? string.Empty;
    }
  }
}

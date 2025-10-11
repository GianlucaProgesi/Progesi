#nullable disable
using System;
using System.Reflection;
using Grasshopper.Kernel;
using ProgesiGrasshopperAssembly.Infrastructure;

namespace ProgesiGrasshopperAssembly.Components
{
  public sealed class ProgesiSnipComponent : GH_Component
  {
    public ProgesiSnipComponent()
      : base("ProgesiSnip", "Snip",
             "Normalizza un input in formato Snip.\n" +
             "Accetta: snip:*, data-url base64, path/URL immagine, base64 'nuda'.",
             "Progesi", "Utils")
    { }

    public override Guid ComponentGuid => new Guid("B7A2DF35-5F8E-43E1-9FBB-2EBF572A4E6B");
    protected override System.Drawing.Bitmap Icon => ProgesiIcons.Snip;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Esegui (default TRUE).", GH_ParamAccess.item, true);
      p.AddGenericParameter("Input", "Input", "snip:*/data-url/base64/path/url (accetta anche File Path).", GH_ParamAccess.item);
      p.AddTextParameter("Cap", "Cap", "Caption (verrà sanitizzato).", GH_ParamAccess.item, "");
      // Idx rimosso: l'indice è gestito a valle (qui fissiamo 1)
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

      string outSnip = ""; string outInfo = "";

      if (!run) { Emit(DA, outSnip, "Idle"); return; }

      // Estrai una stringa dall'input in modo robusto (supporta GH_Goo, File Path, ecc.)
      string s = ExtractString(input);
      if (string.IsNullOrWhiteSpace(s))
      {
        Emit(DA, outSnip, "Input snip vuoto");
        return;
      }

      // indice sempre 1: l'eventuale 'multi-snips' viene gestito dal consumer
      const int index = 1;

      try
      {
        if (SnipHelpers.TryMake(s, cap ?? "", index, out outSnip, out outInfo))
        {
          if (string.IsNullOrWhiteSpace(outInfo)) outInfo = "OK";
          Emit(DA, outSnip, outInfo);
        }
        else
        {
          // TryMake ha già messo il motivo (es. “Formato Snip non riconosciuto”)
          Emit(DA, outSnip, string.IsNullOrWhiteSpace(outInfo) ? "Invalid Snip" : outInfo);
        }
      }
      catch (Exception ex)
      {
        Emit(DA, outSnip, "Errore: " + ex.Message);
      }
    }

    // ---- helpers ----
    private static string ExtractString(object input)
    {
      if (input == null) return string.Empty;

      // 1) se è già stringa
      if (input is string ss) return ss;

      // 2) se è un GH_Goo, prova CastTo<string>
      // (evitiamo dipendenze dirette da GH_IO: usiamo reflection se disponibile)
      var t = input.GetType();
      var iface = t.GetInterface("Grasshopper.Kernel.Types.IGH_Goo");
      if (iface != null)
      {
        object[] args = new object[] { null };
        var m = t.GetMethod("CastTo", new Type[] { typeof(object).MakeByRefType() });
        if (m != null)
        {
          var ok = (bool)m.Invoke(input, args);
          if (ok && args[0] is string gooStr) return gooStr;
        }

        // fallback: prova proprietà "Value" se esiste
        var vp = t.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
        if (vp != null)
        {
          var v = vp.GetValue(input, null);
          if (v != null) return v.ToString();
        }
      }

      // 3) proprietà Value (alcuni wrapper banalizzati)
      var pVal = t.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
      if (pVal != null)
      {
        var v = pVal.GetValue(input, null);
        if (v != null) return v.ToString();
      }

      // 4) fallback definitivo
      return input.ToString() ?? string.Empty;
    }

    private static void Emit(IGH_DataAccess DA, string snip, string info)
    {
      DA.SetData(0, snip ?? "");
      DA.SetData(1, info ?? "");
    }
  }
}

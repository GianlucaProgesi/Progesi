#nullable disable
using System;
using System.Drawing;
using System.Linq;
using System.Reflection;

namespace ProgesiGrasshopperAssembly.Infrastructure
{
  internal static class ProgesiIcons
  {
    private static Bitmap Load(string file)
    {
      try
      {
        var asm = Assembly.GetExecutingAssembly();
        // nome atteso con LogicalName
        var expected = "ProgesiGrasshopperAssembly.Resources." + file;

        var res = asm.GetManifestResourceNames()
                     .FirstOrDefault(n => n.Equals(expected, StringComparison.OrdinalIgnoreCase));

        // fallback tollerante
        if (res == null)
          res = asm.GetManifestResourceNames()
                   .FirstOrDefault(n => n.EndsWith(".Resources." + file, StringComparison.OrdinalIgnoreCase));

        if (res != null)
        {
          using (var s = asm.GetManifestResourceStream(res))
            return (Bitmap)Image.FromStream(s);
        }
      }
      catch { }
      return null;
    }

    public static Bitmap MetIn => Load("metin.png");
    public static Bitmap MetOut => Load("metout.png");
    public static Bitmap VarIn => Load("varin.png");
    public static Bitmap VarOut => Load("varout.png");
    public static Bitmap Snip => Load("snip.png");

    public static Bitmap DataEx => Load("dataex.png");
  }
}

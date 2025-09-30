using System.Drawing;

namespace ProgesiGrasshopperAssembly.Infrastructure
{
  internal static class ProgesiIcons
  {
    // nullable per evitare l'obbligo di inizializzazione nel costruttore di tipo
    private static Bitmap? _px;

    public static Bitmap Transparent1px
    {
      get
      {
        if (_px == null)
        {
          _px = new Bitmap(1, 1);
          _px.SetPixel(0, 0, Color.Transparent);
        }
        return _px!; // dopo il check non è più null
      }
    }
  }
}

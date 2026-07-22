// ServiceHub.cs
#nullable disable
using System;
using Rhino;

namespace ProgesiGrasshopperAssembly.Infrastructure
{
  /// <summary>
  /// Wiring minimale per i componenti GH: solo RHINO (come da S1).
  /// </summary>
  internal static class ServiceHub
  {
    internal sealed class RhinoContext
    {
      public RhinoDoc Doc { get; }
      public RhinoContext(RhinoDoc doc) { Doc = doc ?? throw new ArgumentNullException(nameof(doc)); }
    }

    public static bool TryGetMetadataRepository(out object repoObj, out string info)
    {
      var active = RhinoDoc.ActiveDoc;
      if (active != null)
      {
        repoObj = new RhinoContext(active);
        info = "RHINO";
        return true;
      }
      repoObj = null;
      info = "OK (nessun repo collegato)";
      return false;
    }
  }
}

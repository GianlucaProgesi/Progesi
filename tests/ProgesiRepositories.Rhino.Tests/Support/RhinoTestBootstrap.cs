using System;
using Rhino.Geometry;

namespace ProgesiRepositories.Rhino.Tests.Support
{
  internal static class RhinoTestBootstrap
  {
    private static bool _probed;
    private static bool _available;
    private static string? _reason;

    public static bool IsAvailable => Probe();

    public static string? UnavailableReason => _reason;

    public static void Require()
    {
      if (!Probe())
        throw new InvalidOperationException(_reason ?? "Rhino native runtime is unavailable.");
    }

    private static bool Probe()
    {
      if (_probed) return _available;
      _probed = true;
      try
      {
        var line = new LineCurve(Point3d.Origin, new Point3d(1, 0, 0));
        _available = line.IsValid && line.GetLength() > 0;
        if (!_available)
          _reason = "Rhino geometry probe failed.";
      }
      catch (Exception ex)
      {
        _available = false;
        _reason = ex.Message;
      }
      return _available;
    }
  }
}

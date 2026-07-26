using Rhino.FileIO;
using Rhino.Geometry;
using System;

#nullable enable
namespace ProgesiRepositories.Rhino
{
  /// <summary>
  /// RhinoCommon geometry persistence codec (ToJSON / FromJSON).
  /// Lives in ProgesiRepositories.Rhino to keep GhExcelReadContract Rhino-free.
  /// </summary>
  public static class ProgesiGeometryValueCodec
  {
    public const string StorageValueTypePrefix = "Progesi.Geometry:";

    public static bool IsGeometry(object? obj) => obj is GeometryBase;

    public static bool IsGeometryValueType(string? valueType)
    {
      if (string.IsNullOrWhiteSpace(valueType))
        return false;

      if (valueType.StartsWith(StorageValueTypePrefix, StringComparison.OrdinalIgnoreCase))
        return true;

      var type = Type.GetType(valueType, throwOnError: false);
      return type != null && typeof(GeometryBase).IsAssignableFrom(type);
    }

    public static string GetStorageValueType(GeometryBase geometry)
    {
      if (geometry == null) throw new ArgumentNullException(nameof(geometry));
      return StorageValueTypePrefix + geometry.GetType().FullName;
    }

    public static string GetShortTypeName(GeometryBase geometry)
    {
      if (geometry == null) throw new ArgumentNullException(nameof(geometry));
      return geometry.GetType().FullName ?? "Rhino.Geometry.GeometryBase";
    }

    public static string Encode(GeometryBase geometry)
    {
      if (geometry == null) throw new ArgumentNullException(nameof(geometry));
      return geometry.ToJSON(new SerializationOptions());
    }

    public static bool TryDecode(string? json, out GeometryBase? geometry)
    {
      geometry = null;
      if (string.IsNullOrWhiteSpace(json))
        return false;

      try
      {
        var obj = GeometryBase.FromJSON(json);
        geometry = obj as GeometryBase;
        return geometry != null;
      }
      catch
      {
        return false;
      }
    }
  }
}

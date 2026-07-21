using System;
using System.Collections.Generic;

namespace Progesi.GhExcelReadContract
{
  public enum GhExcelVariableValueKind
  {
    Primitive,
    UnsupportedMarker,
    NonPrimitiveJsonLeak,
    ReferencedObjectDisplayString
  }

  /// <summary>
  /// GH Excel variable Value/ValC contract: primitives round-trip; Rhino/non-primitive values are explicit unsupported.
  /// </summary>
  public static class GhExcelVariableValueSupport
  {
    public const string UnsupportedPrefix = "@UNSUPPORTED:";

    private static readonly HashSet<string> KnownRhinoReferenceDisplayValues =
      new HashSet<string>(StringComparer.OrdinalIgnoreCase)
      {
        "Referenced Brep",
        "Referenced Planar Curve",
        "Referenced Curve",
        "Referenced Mesh",
        "Referenced Surface",
        "Referenced Point",
        "Referenced Polyline",
        "Referenced Arc",
        "Referenced Circle",
        "Referenced Line",
        "Referenced Extrusion",
        "Referenced SubD",
        "Referenced Polysurface",
        "Referenced Hatch",
      };

    public static bool IsPrimitiveValueType(string valueType)
    {
      var normalized = (valueType ?? "string").Trim().ToLowerInvariant();
      return normalized == "string"
          || normalized == "int"
          || normalized == "long"
          || normalized == "double"
          || normalized == "bool"
          || normalized == "null";
    }

    public static bool IsRhinoObjectLikeValueType(string valueType)
    {
      if (string.IsNullOrWhiteSpace(valueType))
        return false;

      var typeName = valueType.Trim();
      return typeName.IndexOf("Rhino.", StringComparison.OrdinalIgnoreCase) >= 0
          || typeName.IndexOf("ObjectRef", StringComparison.OrdinalIgnoreCase) >= 0
          || typeName.IndexOf("Geometry", StringComparison.OrdinalIgnoreCase) >= 0
          || typeName.IndexOf("DocObjects", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool RequiresUnsupportedExportHandling(string valueType)
    {
      return !IsPrimitiveValueType(valueType);
    }

    /// <summary>
    /// True when export must emit an explicit unsupported marker instead of the raw stored value.
    /// </summary>
    public static bool RequiresUnsupportedExportHandling(string valueType, string rawValue)
    {
      if (RequiresUnsupportedExportHandling(valueType))
        return true;

      return IsPrimitiveValueType(valueType) && IsKnownRhinoReferenceDisplayValue(rawValue);
    }

    /// <summary>
    /// Known Grasshopper/Rhino object-reference display placeholders (manual smoke: Referenced Brep, Referenced Planar Curve).
    /// Only whitelisted "Referenced &lt;Rhino type&gt;" values are classified; ordinary user strings are not.
    /// </summary>
    public static bool IsKnownRhinoReferenceDisplayValue(string value)
    {
      if (string.IsNullOrWhiteSpace(value))
        return false;

      return KnownRhinoReferenceDisplayValues.Contains(value.Trim());
    }

    /// <summary>
    /// Maps stored variable payload to the GH Excel Value cell. Non-primitives become an explicit marker.
    /// </summary>
    public static string FormatExportValue(string valueType, string rawValue, string valC)
    {
      if (!RequiresUnsupportedExportHandling(valueType, rawValue))
        return rawValue ?? string.Empty;

      if (IsRhinoObjectLikeValueType(valueType) || IsKnownRhinoReferenceDisplayValue(rawValue))
        return UnsupportedPrefix + "RhinoObject";

      return UnsupportedPrefix + "NonPrimitive";
    }

    public static bool IsImportSupported(string excelValue, out GhExcelVariableValueKind kind, out string detail)
    {
      kind = GhExcelVariableValueKind.Primitive;
      detail = string.Empty;

      var value = excelValue ?? string.Empty;
      if (value.StartsWith(UnsupportedPrefix, StringComparison.OrdinalIgnoreCase))
      {
        kind = GhExcelVariableValueKind.UnsupportedMarker;
        detail = value.Substring(UnsupportedPrefix.Length).Trim();
        return false;
      }

      if (IsKnownRhinoReferenceDisplayValue(value))
      {
        kind = GhExcelVariableValueKind.ReferencedObjectDisplayString;
        detail = "RhinoObject";
        return false;
      }

      var trimmed = value.TrimStart();
      if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
      {
        kind = GhExcelVariableValueKind.NonPrimitiveJsonLeak;
        detail = "JSON-like";
        return false;
      }

      return true;
    }

    public static string BuildImportSkipMessage(int row, GhExcelVariableValueKind kind, string detail)
    {
      switch (kind)
      {
        case GhExcelVariableValueKind.UnsupportedMarker:
          return $"[Var R{row}] VALUE unsupported for Excel import ({detail}) → skip";
        case GhExcelVariableValueKind.NonPrimitiveJsonLeak:
          return $"[Var R{row}] VALUE non-primitive JSON not supported for Excel import → skip";
        case GhExcelVariableValueKind.ReferencedObjectDisplayString:
          return $"[Var R{row}] VALUE unsupported for Excel import ({detail}) → skip";
        default:
          return $"[Var R{row}] VALUE unsupported for Excel import → skip";
      }
    }
  }
}

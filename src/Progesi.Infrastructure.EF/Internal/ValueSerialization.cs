using System.Globalization;
using Newtonsoft.Json;

namespace Progesi.Infrastructure.EF.Internal;

internal static class ValueSerialization
{
  public static string TypeOf(object? obj)
  {
    if (obj == null) return "null";
    return obj switch
    {
      string => "string",
      int => "int",
      double => "double",
      bool => "bool",
      _ => obj.GetType().AssemblyQualifiedName ?? "object"
    };
  }

  public static string Stringify(object? obj)
  {
    if (obj == null) return "null";
    return obj switch
    {
      string s => s,
      int i => i.ToString(),
      double d => d.ToString(CultureInfo.InvariantCulture),
      bool b => b ? "true" : "false",
      _ => JsonConvert.SerializeObject(obj) ?? string.Empty
    };
  }

  public static object? ParseValue(string value, string valueType)
  {
    if (valueType == "null") return null;
    return valueType switch
    {
      "string" => value,
      "int" => int.Parse(value),
      "double" => double.Parse(value, CultureInfo.InvariantCulture),
      "bool" => value == "true",
      _ => DeserializeOrReturn(value, valueType)
    };
  }

  private static object DeserializeOrReturn(string value, string typeName)
  {
    var t = Type.GetType(typeName, throwOnError: false);
    if (t == null) return value;
    return JsonConvert.DeserializeObject(value, t) ?? (object)value;
  }
}

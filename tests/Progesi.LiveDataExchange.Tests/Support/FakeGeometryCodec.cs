namespace Progesi.LiveDataExchange.Tests.Support
{
  internal sealed class FakeGeometryCodec : IGeometryValueCodec
  {
    public const string GeometryType = "Rhino.Geometry.LineCurve";

    public bool IsGeometryValueType(string valueType) =>
      !string.IsNullOrWhiteSpace(valueType)
      && valueType.IndexOf("Geometry", System.StringComparison.OrdinalIgnoreCase) >= 0;

    public bool TryGetShortTypeName(object value, out string objectType)
    {
      if (value is string s && s.StartsWith("@OBJECT:"))
      {
        objectType = GeometryType;
        return true;
      }

      objectType = null;
      return false;
    }

    public bool TryEncode(object value, out string objectType, out string payloadJson)
    {
      if (value is string s && s.StartsWith("@OBJECT:"))
      {
        objectType = GeometryType;
        payloadJson = s.Substring("@OBJECT:".Length);
        return true;
      }

      objectType = null;
      payloadJson = null;
      return false;
    }

    public bool TryDecode(string payloadJson, out object geometry)
    {
      if (!string.IsNullOrWhiteSpace(payloadJson))
      {
        geometry = "@OBJECT:" + payloadJson;
        return true;
      }

      geometry = null;
      return false;
    }
  }
}

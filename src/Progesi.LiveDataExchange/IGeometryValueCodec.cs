namespace Progesi.LiveDataExchange
{
  /// <summary>
  /// Rhino-free geometry payload codec supplied by the host (e.g. GH/Rhino adapter).
  /// </summary>
  public interface IGeometryValueCodec
  {
    bool IsGeometryValueType(string valueType);

    bool TryGetShortTypeName(object value, out string objectType);

    bool TryEncode(object value, out string objectType, out string payloadJson);

    bool TryDecode(string payloadJson, out object geometry);
  }
}

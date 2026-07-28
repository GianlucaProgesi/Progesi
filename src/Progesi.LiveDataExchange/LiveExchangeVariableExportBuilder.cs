using Progesi.GhExcelReadContract;
using ProgesiCore;

namespace Progesi.LiveDataExchange
{
  public static class LiveExchangeVariableExportBuilder
  {
    public static VariableExportRow Build(
      int id,
      string name,
      object typed,
      string rawValue,
      string valueType,
      int[] depends,
      int[] metadataIds,
      bool assumption,
      IGeometryValueCodec geometryCodec)
    {
      string valc = ProgesiHash.CanonicalValue(typed);
      string vt = valueType ?? "string";
      string excelValue;
      string objectType = "";
      string objectPayloadJson = "";
      bool isExcelUnsupported;

      if (geometryCodec != null && geometryCodec.TryEncode(typed, out objectType, out objectPayloadJson))
      {
        excelValue = GhExcelObjectSheet.BuildObjectMarker(objectType);
        isExcelUnsupported = false;
      }
      else
      {
        excelValue = GhExcelVariableValueSupport.FormatExportValue(vt, rawValue ?? "", valc);
        isExcelUnsupported = GhExcelVariableValueSupport.RequiresUnsupportedExportHandling(vt, rawValue ?? "");
      }

      var metadataIdArray = metadataIds ?? System.Array.Empty<int>();
      var pv = new ProgesiVariable(id, name ?? "", typed ?? "", depends ?? System.Array.Empty<int>(), metadataIdArray, assumption);
      string hash = ProgesiHash.Compute(pv);

      return new VariableExportRow
      {
        Id = id,
        Hash = hash,
        Name = name ?? "",
        Value = excelValue,
        ValC = valc,
        MetaId = metadataIdArray.Length > 0 ? metadataIdArray[0] : 0,
        MetadataIds = metadataIdArray,
        Depends = depends ?? System.Array.Empty<int>(),
        Assumption = assumption,
        IsExcelUnsupported = isExcelUnsupported,
        ObjectType = objectType,
        ObjectPayloadJson = objectPayloadJson
      };
    }
  }
}

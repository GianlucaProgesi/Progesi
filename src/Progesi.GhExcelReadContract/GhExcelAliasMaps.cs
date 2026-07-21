using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Progesi.GhExcelReadContract
{
  public static class GhExcelAliasMaps
  {
    public static (Dictionary<string, HashSet<string>> VariableAliases,
                   Dictionary<string, HashSet<string>> MetadataAliases)
      Build(string mapJson)
    {
      var variableAliases = CreateDefaultVariableAliases();
      var metadataAliases = CreateDefaultMetadataAliases();

      if (!string.IsNullOrWhiteSpace(mapJson))
      {
        try
        {
          var json = JObject.Parse(mapJson);
          if (json["Variables"] is JObject variableOverrides)
            MergeAliases(variableOverrides, variableAliases);
          if (json["Metadata"] is JObject metadataOverrides)
            MergeAliases(metadataOverrides, metadataAliases);
        }
        catch
        {
          // ignore malformed map JSON (same behaviour as GH component)
        }
      }

      return (variableAliases, metadataAliases);
    }

    public static Dictionary<string, HashSet<string>> CreateDefaultVariableAliases()
    {
      return new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
      {
        ["ID"] = new HashSet<string>(new[] { "ID", "IDVAR", "VARID" }, StringComparer.OrdinalIgnoreCase),
        ["HASH"] = new HashSet<string>(new[] { "HASH", "DIGEST", "SHA" }, StringComparer.OrdinalIgnoreCase),
        ["NAME"] = new HashSet<string>(new[] { "NAME", "VAR", "VARIABLE", "NOME", "FIELD" }, StringComparer.OrdinalIgnoreCase),
        ["VALUE"] = new HashSet<string>(new[] { "VALUE", "VAL", "VALORE" }, StringComparer.OrdinalIgnoreCase),
        ["VALC"] = new HashSet<string>(new[] { "VALC", "VALUECANONICAL", "VAL_CANONICAL", "CANONICAL" }, StringComparer.OrdinalIgnoreCase),
        ["METAID"] = new HashSet<string>(new[] { "METAID", "MID", "METADATAID", "META_ID" }, StringComparer.OrdinalIgnoreCase),
        ["DEPENDS"] = new HashSet<string>(new[] { "DEPENDS", "DEPENDENCIES", "DEPS", "DEP", "PARENT_IDS" }, StringComparer.OrdinalIgnoreCase),
        ["ASSUMPTION"] = new HashSet<string>(new[] { "ASSUMPTION", "ASS", "ISASSUMPTION", "ASSUME" }, StringComparer.OrdinalIgnoreCase)
      };
    }

    public static Dictionary<string, HashSet<string>> CreateDefaultMetadataAliases()
    {
      return new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
      {
        ["ID"] = new HashSet<string>(new[] { "ID", "METAID" }, StringComparer.OrdinalIgnoreCase),
        ["HASH"] = new HashSet<string>(new[] { "HASH", "DIGEST" }, StringComparer.OrdinalIgnoreCase),
        ["BY"] = new HashSet<string>(new[] { "BY", "AUTHOR", "CREATEDBY", "CREATED_BY", "OWNER" }, StringComparer.OrdinalIgnoreCase),
        ["DESCRIPTION"] = new HashSet<string>(new[] { "DESCRIPTION", "DESC", "DESCR", "INFO", "NOTE", "NOTES" }, StringComparer.OrdinalIgnoreCase),
        ["REFS"] = new HashSet<string>(new[] { "REFS", "REF", "REFERENCE", "REFERENCES", "URLS", "LINKS" }, StringComparer.OrdinalIgnoreCase),
        ["SNIPS"] = new HashSet<string>(new[] { "SNIPS", "SNIP", "ATTACHMENTS", "IMAGES" }, StringComparer.OrdinalIgnoreCase),
        ["LM"] = new HashSet<string>(new[] { "LM", "LASTMODIFIED", "LAST_MODIFIED", "UPDATED", "LASTUPDATE", "LAST_UPDATE" }, StringComparer.OrdinalIgnoreCase)
      };
    }

    public static Dictionary<string, HashSet<string>> CreateDefaultClusterAliases()
    {
      return new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
      {
        ["ID"] = new HashSet<string>(new[] { "ID", "CLUSTERID", "CID" }, StringComparer.OrdinalIgnoreCase),
        ["HASH"] = new HashSet<string>(new[] { "HASH", "DIGEST", "HASHTAG" }, StringComparer.OrdinalIgnoreCase),
        ["NAME"] = new HashSet<string>(new[] { "NAME", "CLUSTER", "CLUSTERNAME", "NOME" }, StringComparer.OrdinalIgnoreCase),
        ["DESCRIPTION"] = new HashSet<string>(new[] { "DESCRIPTION", "DESC", "DESCR", "INFO", "NOTE", "NOTES" }, StringComparer.OrdinalIgnoreCase),
        ["VARIABLEIDS"] = new HashSet<string>(new[] { "VARIABLEIDS", "VARIDS", "VARIABLES", "VARS", "MEMBERIDS", "MEMBERS" }, StringComparer.OrdinalIgnoreCase)
      };
    }

    public static string NormalizeKey(string value)
    {
      if (string.IsNullOrEmpty(value))
        return string.Empty;

      var upper = value.Trim().ToUpperInvariant();
      var buffer = new StringBuilder(upper.Length);
      for (int i = 0; i < upper.Length; i++)
      {
        if (char.IsLetterOrDigit(upper[i]))
          buffer.Append(upper[i]);
      }

      return buffer.ToString();
    }

    private static void MergeAliases(JObject source, Dictionary<string, HashSet<string>> target)
    {
      foreach (var property in source.Properties())
      {
        var key = NormalizeKey(property.Name);
        if (string.IsNullOrEmpty(key))
          continue;

        if (!target.ContainsKey(key))
          target[key] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (property.Value is JArray array)
        {
          foreach (var token in array)
          {
            var alias = token?.ToString();
            if (!string.IsNullOrWhiteSpace(alias))
              target[key].Add(NormalizeKey(alias));
          }
        }
      }
    }
  }
}

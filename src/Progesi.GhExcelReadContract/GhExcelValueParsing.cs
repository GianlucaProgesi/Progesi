using System;
using System.Collections.Generic;
using System.Globalization;

namespace Progesi.GhExcelReadContract
{
  public static class GhExcelValueParsing
  {
    public static bool IsBlank(string value) => string.IsNullOrWhiteSpace(value);

    public static int ToInt(string value)
    {
      int number;
      return int.TryParse((value ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
        ? number
        : 0;
    }

    public static bool ToBool(string value)
    {
      var trimmed = (value ?? string.Empty).Trim();
      if (trimmed == "1") return true;
      if (trimmed == "0") return false;
      bool parsed;
      return bool.TryParse(trimmed, out parsed) && parsed;
    }

    public static int[] ParseDepends(string value)
    {
      if (string.IsNullOrWhiteSpace(value))
        return Array.Empty<int>();

      var tokens = value.Split(new[] { ',', ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries);
      var list = new List<int>();
      foreach (var token in tokens)
      {
        int number;
        if (int.TryParse(token.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number) && number > 0)
          list.Add(number);
      }

      list.Sort();
      return list.ToArray();
    }
  }
}

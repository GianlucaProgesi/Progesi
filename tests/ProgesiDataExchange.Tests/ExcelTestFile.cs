using System;
using System.IO;

namespace Progesi.DataExchange.Tests
{
  internal sealed class ExcelTestFile : IDisposable
  {
    public string Path { get; }

    public ExcelTestFile(string? suffix = null)
    {
      Path = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(),
        $"progesi-dataex-test-{Guid.NewGuid():N}{suffix ?? ".xlsx"}");
    }

    public void Dispose()
    {
      try
      {
        if (File.Exists(Path))
          File.Delete(Path);
      }
      catch
      {
        // best-effort temp cleanup
      }
    }
  }
}

using System;
using System.IO;

namespace Progesi.GhExcelReadContract.Tests
{
  internal sealed class ExcelTestFile : IDisposable
  {
    public string Path { get; }

    public ExcelTestFile()
    {
      Path = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(),
        $"progesi-gh-readcontract-{Guid.NewGuid():N}.xlsx");
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

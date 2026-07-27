using Progesi.Infrastructure.EF;

namespace Progesi.Stress.Tests;

internal static class EfTestBootstrap
{
  internal static string CreateTempFileConnectionString()
  {
    var path = Path.Combine(Path.GetTempPath(), $"progesi_stress_ef_{Guid.NewGuid():N}.sqlite");
    return $"Data Source={path}";
  }

  internal static void TryDeleteFile(string connectionString)
  {
    var path = connectionString.Replace("Data Source=", string.Empty);
    try
    {
      if (File.Exists(path))
        File.Delete(path);
    }
    catch
    {
      // best-effort cleanup
    }
  }
}

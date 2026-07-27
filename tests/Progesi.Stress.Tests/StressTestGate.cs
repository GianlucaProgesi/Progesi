using System.Diagnostics;
using Xunit.Abstractions;

namespace Progesi.Stress.Tests;

internal static class StressTestGate
{
  internal const string SkipMessage = "set PROGESI_STRESS=1 to run stress tests";

  internal static void RequireEnabled()
  {
    Skip.IfNot(Environment.GetEnvironmentVariable("PROGESI_STRESS") == "1", SkipMessage);
  }

  internal static int ScaleN()
  {
    var raw = Environment.GetEnvironmentVariable("PROGESI_STRESS_N");
    if (int.TryParse(raw, out var n) && n > 0)
      return n;
    return 10_000;
  }

  internal static void LogTiming(ITestOutputHelper output, string label, Stopwatch sw, int count)
  {
    var ms = sw.Elapsed.TotalMilliseconds;
    var rate = count > 0 && ms > 0 ? count / (ms / 1000.0) : 0;
    output.WriteLine($"{label}: {ms:F0} ms, count={count}, ~{rate:F0}/s");
  }
}

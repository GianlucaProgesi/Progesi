using System;

namespace ProgesiGrasshopperAssembly.Infrastructure
{
  internal static class InfoMessages
  {
    public static string Ok(string? tail = null)
        => tail is null ? "OK" : $"OK ({tail})";

    public static string Idle() => "Idle";

    public static string NotFound(string? hint = null)
        => hint is null ? "Non trovato" : $"Non trovato: {hint}";

    public static string Invalid(string reason)
        => $"Input non valido: {reason}";

    public static string RepoMissing(bool mockOn, string? mockRoot)
    {
      if (mockOn && !string.IsNullOrWhiteSpace(mockRoot))
        return $"OK (mock: {mockRoot})"; // stato atteso in P0 quando non c'è DB
      return "OK (nessun repo collegato)";
    }

    public static string WithDuration(string baseMsg, TimeSpan? elapsed) =>
        elapsed is null ? baseMsg : $"{baseMsg} [{elapsed.Value.TotalMilliseconds:0} ms]";
  }
}

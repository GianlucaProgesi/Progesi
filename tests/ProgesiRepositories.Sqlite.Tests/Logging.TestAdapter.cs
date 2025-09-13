// tests/ProgesiRepositories.Sqlite.Tests/Logging.TestAdapter.cs
#nullable enable
using System;
using Microsoft.Extensions.Logging;
using ProgesiRepositories.Sqlite;

namespace ProgesiRepositories.Sqlite.Tests
{
  /// <summary>
  /// Adattatore di test: consente di usare un ILogger come IProgesiLogger.
  /// </summary>
  internal sealed class TestProgesiLoggerAdapter : IProgesiLogger
  {
    private readonly ILogger _inner;

    public TestProgesiLoggerAdapter(ILogger? inner = null)
    {
      _inner = inner ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    }

    // --- Adatta qui ai metodi effettivi della tua interfaccia IProgesiLogger, se differiscono ---

    public void Info(string message) => _inner.LogInformation(message);
    public void Debug(string message) => _inner.LogDebug(message);
    public void Warn(string message) => _inner.LogWarning(message);

    public void Error(string message) => _inner.LogError(message);
    public void Error(Exception ex) => _inner.LogError(ex, ex.Message);
    public void Error(Exception ex, string message) => _inner.LogError(ex, message);

    public void Log(string message) => _inner.LogInformation(message);

    public IDisposable BeginScope(string name)
    {
      // ILogger.BeginScope non dovrebbe restituire null, ma gestiamo comunque il caso per zittire i warning.
      var disp = _inner.BeginScope(name);
      return disp ?? NoopDisposable.Instance;
    }

    private sealed class NoopDisposable : IDisposable
    {
      public static readonly NoopDisposable Instance = new NoopDisposable();
      private NoopDisposable() { }
      public void Dispose() { }
    }
  }

  internal static class LoggerTestExtensions
  {
    /// <summary>Converte qualsiasi ILogger in IProgesiLogger tramite l’adapter di test.</summary>
    public static IProgesiLogger ToProgesiLogger(this ILogger logger)
        => new TestProgesiLoggerAdapter(logger);
  }
}

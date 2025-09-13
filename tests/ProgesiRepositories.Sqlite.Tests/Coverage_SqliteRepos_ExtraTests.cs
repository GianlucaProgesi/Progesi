#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace ProgesiRepositories.Sqlite.Tests
{
  internal sealed class NoopLogger<T> : ILogger<T>, ILogger
  {
    public IDisposable BeginScope<TState>(TState state) where TState : notnull
        => NoopDisposable.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
      // no-op
    }

    private sealed class NoopDisposable : IDisposable
    {
      public static readonly NoopDisposable Instance = new NoopDisposable();
      private NoopDisposable() { }
      public void Dispose() { }
    }
  }
}

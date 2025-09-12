using System;

namespace ProgesiRepositories.Sqlite
{
  public interface IProgesiLogger
  {
    void Debug(string message);
    void Info(string message);
    void Warn(string message);
    void Error(string message);
    void Error(Exception ex, string message);
  }

  public sealed class NullLogger : IProgesiLogger
  {
    public static readonly NullLogger Instance = new NullLogger();
    private NullLogger() { }

    public void Debug(string message) { }
    public void Info(string message) { }
    public void Warn(string message) { }
    public void Error(string message) { }
    public void Error(Exception ex, string message) { }
  }
}

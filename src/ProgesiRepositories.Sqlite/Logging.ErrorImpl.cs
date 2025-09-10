using System;

namespace ProgesiRepositories.Sqlite
{
    public partial class TraceLogger : IProgesiLogger
    {
        public void Error(string message) => Warn(message);
        public void Error(Exception ex, string message) => Warn($"{message} :: {ex}");
    }

    public partial class FileLogger : IProgesiLogger
    {
        public void Error(string message) => Warn(message);
        public void Error(Exception ex, string message) => Warn($"{message} :: {ex}");
    }

    public partial class RollingFileLogger : IProgesiLogger
    {
        public void Error(string message) => Warn(message);
        public void Error(Exception ex, string message) => Warn($"{message} :: {ex}");
    }
}

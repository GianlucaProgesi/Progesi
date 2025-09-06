#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace ProgesiRepositories.Sqlite
{
    /// <summary>Logger che scrive su System.Diagnostics.Trace.</summary>
    public sealed partial class TraceLogger : IProgesiLogger
    {
        private static string Now() => DateTime.UtcNow.ToString("o");
        private static string Prefix(string level) => $"{Now()} [{level}]";

        public void Debug(string message) => Trace.WriteLine($"{Prefix("DBG")} {message}");
        public void Info(string message) => Trace.WriteLine($"{Prefix("INF")} {message}");
        public void Warn(string message) => Trace.WriteLine($"{Prefix("WRN")} {message}");
        public void Error(string message, Exception? ex = null)
        {
            Trace.WriteLine($"{Prefix("ERR")} {message}");
            if (ex != null) Trace.WriteLine(ex.ToString());
        }
    }

    /// <summary>Logger file-based semplice e thread-safe (append).</summary>
    public sealed partial class FileLogger : IProgesiLogger, IDisposable
    {
        private readonly string _path;
        private readonly object _lock = new object();
        private readonly Encoding _enc = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public FileLogger(string path)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir!);
        }

        private static string Now() => DateTime.UtcNow.ToString("o");
        private static string Prefix(string level) => $"{Now()} [{level}]";

        private void WriteLine(string line)
        {
            lock (_lock)
            {
                File.AppendAllText(_path, line + Environment.NewLine, _enc);
            }
        }

        public void Debug(string message) => WriteLine($"{Prefix("DBG")} {message}");
        public void Info(string message) => WriteLine($"{Prefix("INF")} {message}");
        public void Warn(string message) => WriteLine($"{Prefix("WRN")} {message}");
        public void Error(string message, Exception? ex = null)
        {
            WriteLine($"{Prefix("ERR")} {message}");
            if (ex != null) WriteLine(ex.ToString());
        }

        public void Dispose() { /* nothing to dispose, kept for symmetry */ }
    }
}


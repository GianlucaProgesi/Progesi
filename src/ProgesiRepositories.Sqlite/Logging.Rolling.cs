#nullable enable
using System;
using System.IO;
using System.Text;

namespace ProgesiRepositories.Sqlite
{
    /// <summary>
    /// Logger file-based con rotazione per dimensione.
    /// - Thread-safe
    /// - Rotazione: file -> .1 -> .2 ... fino a maxFiles
    /// - No esterne dipendenze
    /// </summary>
    public sealed partial class RollingFileLogger : IProgesiLogger, IDisposable
    {
        private readonly string _path;
        private readonly long _maxBytes;
        private readonly int _maxFiles;
        private readonly object _lock = new object();
        private readonly Encoding _enc = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        /// <param name="path">Percorso del file di log principale (es: C:\logs\progesi\app.log)</param>
        /// <param name="maxBytes">Dimensione max prima della rotazione (default: 5 MiB)</param>
        /// <param name="maxFiles"># file di storia mantenuti (default: 5)</param>
        public RollingFileLogger(string path, long maxBytes = 5 * 1024 * 1024, int maxFiles = 5, bool rollOnStartup = false)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
            if (maxBytes < 1024) throw new ArgumentOutOfRangeException(nameof(maxBytes), "Minimo 1 KiB.");
            if (maxFiles < 1) throw new ArgumentOutOfRangeException(nameof(maxFiles), "Minimo 1.");

            _path = path;
            _maxBytes = maxBytes;
            _maxFiles = maxFiles;

            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir!);

            if (rollOnStartup && File.Exists(_path))
                RotateLocked();
        }

        private static string Now() => DateTime.UtcNow.ToString("o");
        private static string Prefix(string level) => $"{Now()} [{level}]";

        private void Write(string line)
        {
            lock (_lock)
            {
                // scrivi
                File.AppendAllText(_path, line + Environment.NewLine, _enc);

                // controlla dimensione
                try
                {
                    var info = new FileInfo(_path);
                    if (info.Exists && info.Length >= _maxBytes)
                        RotateLocked();
                }
                catch
                {
                    // non bloccare l'app se il file system è momentaneamente non disponibile
                }
            }
        }

        private void RotateLocked()
        {
            // sposta .(max-1) -> .(max) e così via
            for (int i = _maxFiles - 1; i >= 1; i--)
            {
                var src = $"{_path}.{i}";
                var dst = $"{_path}.{i + 1}";
                if (File.Exists(dst))
                {
                    try { File.Delete(dst); } catch { }
                }
                if (File.Exists(src))
                {
                    try { File.Move(src, dst); } catch { }
                }
            }

            // principale -> .1
            var first = $"{_path}.1";
            if (File.Exists(first))
            {
                try { File.Delete(first); } catch { }
            }
            if (File.Exists(_path))
            {
                try { File.Move(_path, first); }
                catch
                {
                    // fallback: se Move fallisce (lock?), prova a copiare e troncare
                    try
                    {
                        File.Copy(_path, first, overwrite: true);
                        using (var fs = new FileStream(_path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                        {
                            // tronca
                        }
                    }
                    catch { }
                }
            }
        }

        public void Debug(string message) => Write($"{Prefix("DBG")} {message}");
        public void Info(string message) => Write($"{Prefix("INF")} {message}");
        public void Warn(string message) => Write($"{Prefix("WRN")} {message}");
        public void Error(string message, Exception? ex = null)
        {
            Write($"{Prefix("ERR")} {message}");
            if (ex != null) Write(ex.ToString());
        }

        public void Dispose() { /* niente da rilasciare */ }
    }
}


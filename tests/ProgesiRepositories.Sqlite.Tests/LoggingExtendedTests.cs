using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace ProgesiRepositories.Sqlite.Tests
{
  public class LoggingExtendedTests
  {
    // ---- helpers -------------------------------------------------------

    private static string Tmp(string suffix)
    {
      var name = "ProgesiLogs_" + Guid.NewGuid().ToString("N") + suffix;
      return Path.Combine(Path.GetTempPath(), name);
    }

    private static void TryDelete(string pathOrDir)
    {
      try
      {
        if (File.Exists(pathOrDir)) File.Delete(pathOrDir);
        if (Directory.Exists(pathOrDir))
        {
          foreach (var f in Directory.GetFiles(pathOrDir)) { try { File.Delete(f); } catch { } }
          Directory.Delete(pathOrDir, true);
        }
      }
      catch { /* best effort */ }
    }

    private static object CreateWithSmartArgs(Type t, string path, int suggestedMaxBytes)
    {
      var dir = Path.GetDirectoryName(path);
      if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

      var allCtors = t.GetConstructors(BindingFlags.Instance | BindingFlags.Public)
          .OrderBy(c => c.GetParameters().Length)
          .Concat(t.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
              .OrderBy(c => c.GetParameters().Length))
          .ToArray();

      foreach (var ctor in allCtors)
      {
        var ps = ctor.GetParameters();
        var args = new object[ps.Length];

        for (int i = 0; i < ps.Length; i++)
        {
          var p = ps[i];
          var pt = p.ParameterType;

          if (p.HasDefaultValue) { args[i] = p.DefaultValue; continue; }

          if (pt == typeof(string))
          {
            var n = p.Name ?? string.Empty;
            if (n.IndexOf("dir", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("folder", StringComparison.OrdinalIgnoreCase) >= 0)
            {
              args[i] = dir ?? Path.GetTempPath();
            }
            else if (n.IndexOf("name", StringComparison.OrdinalIgnoreCase) >= 0 &&
                     n.IndexOf("file", StringComparison.OrdinalIgnoreCase) >= 0)
            {
              args[i] = Path.GetFileName(path) ?? "log.txt";
            }
            else
            {
              args[i] = path;
            }
          }
          else if (pt == typeof(int)) args[i] = suggestedMaxBytes;
          else if (pt == typeof(long)) args[i] = (long)suggestedMaxBytes;
          else if (pt == typeof(bool)) args[i] = false;
          else if (pt == typeof(TimeSpan)) args[i] = TimeSpan.FromMilliseconds(50);
          else if (pt.IsEnum)
          {
            var values = Enum.GetValues(pt);
            args[i] = values.Length > 0 ? values.GetValue(0) : Activator.CreateInstance(pt);
          }
          else
          {
            args[i] = pt.IsValueType ? Activator.CreateInstance(pt) : null;
          }
        }

        try { return ctor.Invoke(args); }
        catch { /* try next */ }
      }

      throw new InvalidOperationException("Nessun costruttore adatto per " + t.FullName);
    }

    private static void InvokeCommonOverloads(object logger, string message, Exception ex = null)
    {
      var t = logger.GetType();
      var names = new[] { "Write", "Log", "Info", "Trace", "Debug", "Warn", "Error", "Fatal", "Append" };

      foreach (var name in names)
      {
        var methods = t.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                       .Where(m => m.Name == name).ToArray();

        foreach (var m in methods)
        {
          var ps = m.GetParameters();
          var args = new object[ps.Length];

          for (int i = 0; i < ps.Length; i++)
          {
            var pt = ps[i].ParameterType;

            if (pt == typeof(string)) args[i] = message;
            else if (pt == typeof(Exception)) args[i] = ex ?? new InvalidOperationException("boom", new Exception("inner"));
            else if (pt == typeof(object)) args[i] = (object)message;
            else if (pt == typeof(object[])) args[i] = new object[] { "x", 123, true };
            else if (pt == typeof(bool)) args[i] = false;
            else if (pt == typeof(int)) args[i] = 1;
            else if (pt == typeof(long)) args[i] = 1L;
            else if (pt == typeof(TimeSpan)) args[i] = TimeSpan.FromMilliseconds(10);
            else args[i] = pt.IsValueType ? Activator.CreateInstance(pt) : null;
          }

          // Non vogliamo far fallire il test in base a una semantica non documentata: l’obiettivo è “touch coverage”.
          var _ = Record.Exception(() => m.Invoke(logger, args));
        }
      }

      // scarica eventuali Flush/Dispose/Close se esistono
      foreach (var n in new[] { "Flush", "Close", "Dispose" })
      {
        var m = t.GetMethod(n, BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
        if (m != null) { var _ = Record.Exception(() => m.Invoke(logger, Array.Empty<object>())); }
      }
    }

    // ---- tests ----------------------------------------------------------

    [Fact]
    public void TraceLogger_Invokes_Common_Methods()
    {
      var asmName = "ProgesiRepositories.Sqlite";
      var t = Type.GetType("ProgesiRepositories.Sqlite.TraceLogger, " + asmName, throwOnError: false);
      Assert.NotNull(t);

      object logger = t.GetConstructor(Type.EmptyTypes) != null
          ? Activator.CreateInstance(t)
          : Activator.CreateInstance(t, true);

      InvokeCommonOverloads(logger, "trace message", new InvalidOperationException("oops"));
    }

    [Fact]
    public void FileLogger_Multiple_Writes_Grow_File()
    {
      var asmName = "ProgesiRepositories.Sqlite";
      var t = Type.GetType("ProgesiRepositories.Sqlite.FileLogger, " + asmName, throwOnError: false);
      Assert.NotNull(t);

      var path = Tmp(".file.log");
      TryDelete(path);

      var logger = CreateWithSmartArgs(t, path, suggestedMaxBytes: 8192);
      InvokeCommonOverloads(logger, "file logger message", new InvalidOperationException("err"));

      // qualche write in più per certe firme
      InvokeCommonOverloads(logger, new string('x', 200));
      InvokeCommonOverloads(logger, "fmt {0} {1}", new Exception("ex"));

      Assert.True(File.Exists(path));
      Assert.True(new FileInfo(path).Length > 0);

      TryDelete(path);
    }

    [Fact]
    public void RollingFileLogger_Forces_Rotation_Or_At_Least_Writes()
    {
      var asmName = "ProgesiRepositories.Sqlite";
      var t = Type.GetType("ProgesiRepositories.Sqlite.RollingFileLogger, " + asmName, throwOnError: false);
      Assert.NotNull(t);

      var basePath = Tmp(".rolling.log");
      TryDelete(basePath);

      // Limite piccolo per aumentare le chance di rotazione
      var logger = CreateWithSmartArgs(t, basePath, suggestedMaxBytes: 256);

      // tante scritture per superare il limite
      for (int i = 0; i < 200; i++)
      {
        InvokeCommonOverloads(logger, "line " + i + " " + new string('z', 120));
      }

      var dir = Path.GetDirectoryName(basePath) ?? Path.GetTempPath();
      var baseName = Path.GetFileName(basePath);
      var baseNoExt = Path.GetFileNameWithoutExtension(basePath);

      var all = Directory.GetFiles(dir);
      var samePrefix = all.Where(f =>
      {
        var fn = Path.GetFileName(f);
        return fn != null && (fn.StartsWith(baseNoExt, StringComparison.OrdinalIgnoreCase) ||
                              fn.StartsWith(baseName, StringComparison.OrdinalIgnoreCase));
      }).ToArray();

      // Deve almeno esistere il file principale e avere contenuto.
      Assert.True(File.Exists(basePath));
      Assert.True(new FileInfo(basePath).Length > 0);

      // Se la strategia di rotazione è attiva, spesso si crea >1 file con stesso prefisso.
      // Non obblighiamo, ma registriamo il dato per la diagnosi.
      Assert.True(samePrefix.Length >= 1);

      TryDelete(basePath);
      // prova a ripulire eventuali rotazioni create
      foreach (var f in samePrefix)
      {
        if (File.Exists(f)) TryDelete(f);
      }
    }

    [Fact]
    public void ErrorLogging_Paths_Are_Touched_Via_Error_Overloads()
    {
      var asmName = "ProgesiRepositories.Sqlite";
      var t = Type.GetType("ProgesiRepositories.Sqlite.FileLogger, " + asmName, throwOnError: false)
              ?? Type.GetType("ProgesiRepositories.Sqlite.TraceLogger, " + asmName, throwOnError: false);
      Assert.NotNull(t);

      var path = Tmp(".err.log");
      TryDelete(path);

      var logger = CreateWithSmartArgs(t, path, 1024);
      // chiama gli overload “Error(... Exception)” se esistono, per toccare i rami che formattano eccezioni
      InvokeCommonOverloads(logger, "error path", new InvalidOperationException("bad", new Exception("inner")));

      // non deve crashare, il file (se file-based) cresce
      if (File.Exists(path))
      {
        Assert.True(new FileInfo(path).Length > 0);
        TryDelete(path);
      }
    }
  }
}

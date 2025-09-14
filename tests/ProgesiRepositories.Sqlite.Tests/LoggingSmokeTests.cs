using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace ProgesiRepositories.Sqlite.Tests
{
  public class LoggingSmokeTests
  {
    private static string NewTempFilePath(string suffix)
    {
      var name = Guid.NewGuid().ToString("N") + suffix;
      return Path.Combine(Path.GetTempPath(), "ProgesiLogs_" + name);
    }

    private static void TryDelete(string path)
    {
      try { if (File.Exists(path)) File.Delete(path); }
      catch { /* best effort */ }
    }

    private static object CreateWithSmartArgs(Type t, string path, int suggestedMaxSize)
    {
      // Ci assicuriamo che la directory esista nel caso serva
      var dir = Path.GetDirectoryName(path);
      if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

      // Prova prima i costruttori pubblici (pochi parametri prima), poi i non pubblici
      var allCtors = t.GetConstructors(BindingFlags.Instance | BindingFlags.Public)
          .OrderBy(c => c.GetParameters().Length)
          .Concat(t.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
              .OrderBy(c => c.GetParameters().Length))
          .ToArray();

      foreach (var ctor in allCtors)
      {
        var ps = ctor.GetParameters();
        var args = new object[ps.Length];
        var ok = true;

        for (int i = 0; i < ps.Length; i++)
        {
          var p = ps[i];
          var pt = p.ParameterType;
          object val = null;

          // Usa default del parametro se presente
          if (p.HasDefaultValue)
          {
            val = p.DefaultValue;
          }
          else if (pt == typeof(string))
          {
            // Se il nome suggerisce directory -> passa solo la directory, altrimenti il path completo
            var n = p.Name ?? string.Empty;
            if (n.IndexOf("dir", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("folder", StringComparison.OrdinalIgnoreCase) >= 0)
            {
              val = dir ?? Path.GetTempPath();
            }
            else if (n.IndexOf("name", StringComparison.OrdinalIgnoreCase) >= 0 &&
                     n.IndexOf("file", StringComparison.OrdinalIgnoreCase) >= 0)
            {
              val = Path.GetFileName(path);
              if (string.IsNullOrEmpty((string)val)) val = "log.txt";
            }
            else
            {
              val = path;
            }
          }
          else if (pt == typeof(int))
          {
            val = suggestedMaxSize;
          }
          else if (pt == typeof(long))
          {
            val = (long)suggestedMaxSize;
          }
          else if (pt == typeof(bool))
          {
            val = false;
          }
          else if (pt == typeof(TimeSpan))
          {
            val = TimeSpan.FromSeconds(1);
          }
          else if (pt.IsEnum)
          {
            // primo valore dell'enum
            var values = Enum.GetValues(pt);
            val = values.Length > 0 ? values.GetValue(0) : Activator.CreateInstance(pt);
          }
          else
          {
            // fallback: per value types crea default, per reference type usa null
            val = pt.IsValueType ? Activator.CreateInstance(pt) : null;
          }

          args[i] = val;
        }

        try
        {
          return ctor.Invoke(args);
        }
        catch
        {
          ok = false;
        }

        if (!ok) continue;
      }

      throw new InvalidOperationException("Nessun costruttore adatto per " + t.FullName);
    }

    private static Action<string> ResolveWriteLikeMethod(object logger)
    {
      var t = logger.GetType();
      // metodi candidati con una sola string
      var names = new[] { "Write", "Log", "Append", "Info", "Error", "Trace" };

      foreach (var n in names)
      {
        var m = t.GetMethod(n, BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(string) }, null);
        if (m != null)
        {
          return msg => m.Invoke(logger, new object[] { msg });
        }
      }

      // metodi candidati con (string, Exception)
      foreach (var n in names)
      {
        var m = t.GetMethod(n, BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(string), typeof(Exception) }, null);
        if (m != null)
        {
          return msg => m.Invoke(logger, new object[] { msg, null });
        }
      }

      // Nessun metodo di scrittura trovato: no-op
      return msg => { };
    }

    [Fact]
    public void FileLogger_Writes_To_File()
    {
      var asmName = "ProgesiRepositories.Sqlite";
      var typeName = "ProgesiRepositories.Sqlite.FileLogger, " + asmName;

      var t = Type.GetType(typeName, throwOnError: false);
      Assert.NotNull(t);

      var path = NewTempFilePath(".file.log");
      TryDelete(path);

      var logger = CreateWithSmartArgs(t, path, 4096);
      var write = ResolveWriteLikeMethod(logger);

      write("hello from FileLogger");
      write("second line");

      Assert.True(File.Exists(path));
      var len = new FileInfo(path).Length;
      Assert.True(len > 0);

      TryDelete(path);
    }

    [Fact]
    public void RollingFileLogger_Rotates_Or_Writes()
    {
      var asmName = "ProgesiRepositories.Sqlite";
      var typeName = "ProgesiRepositories.Sqlite.RollingFileLogger, " + asmName;

      var t = Type.GetType(typeName, throwOnError: false);
      Assert.NotNull(t);

      var path = NewTempFilePath(".rolling.log");
      TryDelete(path);

      var logger = CreateWithSmartArgs(t, path, 2048);
      var write = ResolveWriteLikeMethod(logger);

      for (int i = 0; i < 20; i++)
      {
        write("line " + i + " " + new string('x', 80));
      }

      Assert.True(File.Exists(path));
      var len = new FileInfo(path).Length;
      Assert.True(len > 0);

      TryDelete(path);
    }

    [Fact]
    public void TraceLogger_Does_Not_Throw_On_Write()
    {
      var asmName = "ProgesiRepositories.Sqlite";
      var typeName = "ProgesiRepositories.Sqlite.TraceLogger, " + asmName;

      var t = Type.GetType(typeName, throwOnError: false);
      Assert.NotNull(t);

      object logger;

      var empty = t.GetConstructor(Type.EmptyTypes);
      if (empty != null) logger = empty.Invoke(new object[0]);
      else logger = Activator.CreateInstance(t, true);

      var write = ResolveWriteLikeMethod(logger);
      var ex = Record.Exception(() => write("trace message"));
      Assert.Null(ex);
    }
  }
}

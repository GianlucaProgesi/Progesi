# ProgesiRepositories.Sqlite  [![NuGet ProgesiRepositories.Sqlite](https://img.shields.io/nuget/v/ProgesiRepositories.Sqlite.svg)](https://www.nuget.org/packages/ProgesiRepositories.Sqlite) ![Downloads ProgesiRepositories.Sqlite](https://img.shields.io/nuget/dt/ProgesiRepositories.Sqlite)   <!-- PROGESI:BODY:START --> SQLite-backed repository for Progesi.  ## Install `dotnet add package ProgesiRepositories.Sqlite`  ## Quick start ```csharp using ProgesiCore; using ProgesiRepositories.Sqlite;  var path = System.IO.Path.Combine(Environment.CurrentDirectory, "progesi.db"); var repo = new SqliteVariablesRepository($"Data Source={path}"); repo.Save(new ProgesiVariable("Span", 35.0)); var v = repo.Get("Span"); Console.WriteLine(v.Value); ```  ## Links - NuGet: https://www.nuget.org/packages/ProgesiRepositories.Sqlite <!-- PROGESI:BODY:END -->

# ProgesiRepositories.Sqlite

Parte della suite **Progesi**.

Installazione:

    dotnet add package ProgesiRepositories.Sqlite

Repository: https://github.com/GianlucaProgesi/Progesi
License: MIT


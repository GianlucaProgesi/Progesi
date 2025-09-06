# ProgesiRepositories.InMemory  [![NuGet ProgesiRepositories.InMemory](https://img.shields.io/nuget/v/ProgesiRepositories.InMemory.svg)](https://www.nuget.org/packages/ProgesiRepositories.InMemory) ![Downloads ProgesiRepositories.InMemory](https://img.shields.io/nuget/dt/ProgesiRepositories.InMemory)   <!-- PROGESI:BODY:START --> In-memory repository implementation for Progesi. Great for tests and prototypes.  ## Install `dotnet add package ProgesiRepositories.InMemory`  ## Quick start ```csharp using ProgesiCore; using ProgesiRepositories.InMemory;  var repo = new InMemoryVariablesRepository(); repo.Save(new ProgesiVariable("Span", 35.0)); var v = repo.Get("Span"); Console.WriteLine(v.Value); ```  ## Links - NuGet: https://www.nuget.org/packages/ProgesiRepositories.InMemory <!-- PROGESI:BODY:END -->

# ProgesiRepositories.InMemory

Parte della suite **Progesi**.

Installazione:

    dotnet add package ProgesiRepositories.InMemory

Repository: https://github.com/GianlucaProgesi/Progesi
License: MIT


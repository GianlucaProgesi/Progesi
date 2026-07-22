# CI per Progesi (build e test)

Esempio di GitHub Actions che compila la soluzione, lancia i test e pubblica un artefatto con il build del plugin.

Crea il file `.github/workflows/build.yml` con:

```yaml
name: build-and-test

on:
  push:
    branches: [ "main", "s2-**" ]
  pull_request:
    branches: [ "main" ]

jobs:
  build:
    runs-on: windows-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Restore
        run: dotnet restore ./Progesi.sln

      - name: Build
        run: dotnet build ./Progesi.sln -c Release --no-restore

      - name: Test
        run: dotnet test ./Progesi.sln -c Release --no-build --verbosity normal

      - name: Publish GH plugin artifact
        if: success()
        run: |
          mkdir artifacts
          xcopy /E /Y ".\src\ProgesiGrasshopperAssembly\bin\Release\net48" ".\artifacts\Progesi-GH"
        shell: cmd

      - name: Upload artifact
        uses: actions/upload-artifact@v4
        with:
          name: progesi-gh-artifact
          path: artifacts/**

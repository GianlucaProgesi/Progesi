# Workflow Previews

## Progesi/.github/workflows/ci.yml

```yaml
name: CI

on:
  push:
    branches:
      - main
      - master
      - develop
      - release/**
  pull_request:

concurrency:
  group: ci-${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: true

permissions:
  contents: read
  actions: read
  checks: write
  pull-requests: write

jobs:
  build-test-coverage:
    name: build-test-coverage
    runs-on: windows-latest
    timeout-minutes: 45

    steps:
      - name: Checkout
        uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Setup .NET SDKs (8.x + 7.0.410)
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: |
            8.x
            7.0.410
          cache: true

      - name: Cache NuGet
        uses: actions/cache@v4
        with:
          path: |
            ~/.nuget/packages
          key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.sln', '**/*.csproj', '**/packages.lock.json') }}
          restore-keys: |
            ${{ runner.os }}-nuget-

      # --- PATCH CHIRURGICA WHITESPACE (Windows) ---
      - name: Normalize line endings (CRLF)
        if: runner.os == 'Windows'
        shell: pwsh
        run: |
          git config core.autocrlf true
          $patterns = @("*.cs","*.csproj","*.sln","*.props","*.targets")
          Get-ChildItem -Recurse -File -Include $patterns | ForEach-Object {
            $p = $_.FullName
            $txt = Get-Content -Raw -LiteralPath $p
            # Uniforma qualsiasi combinazione di EOL in CRLF
            $txt = $txt -replace "(`r)?`n","`r`n"
            [System.IO.File]::WriteAllText($p, $txt, [System.Text.UTF8Encoding]::new($false))
          }

      - name: dotnet format (autofix)
        shell: pwsh
        run: dotnet format --verbosity minimal

      # Se vuoi far fallire i PR se non formattati, tieni questo step.
      # Altrimenti puoi rimuoverlo.
      - name: dotnet format (verify-only on PR)
        if: github.event_name == 'pull_request'
        shell: pwsh
        run: dotnet format --verify-no-changes --verbosity minimal

      - name: Restore
        shell: pwsh
        run: dotnet restore

```
## Progesi/.github/workflows/codeql.yml

```yaml
name: CodeQL

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]
  schedule:
    - cron: '20 1 * * 5'

permissions:
  contents: read
  actions: read
  security-events: write
  pull-requests: read

concurrency:
  group: codeql-${{ github.ref }}
  cancel-in-progress: true

jobs:
  analyze:
    name: Analyze
    runs-on: windows-latest
    if: ${{ github.event_name != 'pull_request' || github.event.pull_request.head.repo.full_name == github.repository }}

    strategy:
      fail-fast: false
      matrix:
        language: [ 'csharp' ]

    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Initialize CodeQL
        uses: github/codeql-action/init@v3
        with:
          languages: ${{ matrix.language }}

      # Autobuild di CodeQL (per C# in genere basta)
      - name: Autobuild
        uses: github/codeql-action/autobuild@v3

      # Se mai servisse una build manuale, decommenta:
      # - run: |
      #     dotnet restore Progesi.sln
      #     dotnet build Progesi.sln -c Release --no-restore

      - name: Perform CodeQL Analysis
        uses: github/codeql-action/analyze@v3
        with:
          category: "/language:${{ matrix.language }}"
```
## Progesi/.github/workflows/labeler.yml

```yaml
name: PR Labeler
on:
  pull_request_target:
    types: [opened, synchronize, reopened, edited]

permissions:
  pull-requests: write

jobs:
  label:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/labeler@v5
        with:
          repo-token: ${{ secrets.GITHUB_TOKEN }}
          configuration-path: .github/labeler.yml
```
## Progesi/.github/workflows/release.yml

```yaml
name: release

on:
  workflow_dispatch:
  push:
    tags:
      - 'v*' # e.g. v1.0.30

permissions:
  contents: write
  packages: write

jobs:
  pack:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build -c Release --no-restore

      - name: Generate packlist (src only, skip Grasshopper)
        shell: bash
        run: |
          set -euo pipefail
          find ./src -name '*.csproj' \
            -not -path '*/ProgesiGrasshopperAssembly/*' \
            -print | sort > packlist.txt
          echo "Pack list:" && cat packlist.txt

      - name: Ensure per-project README placeholder (if missing)
        shell: bash
        run: |
          set -euo pipefail
          while IFS= read -r proj; do
            [[ -z "${proj:-}" ]] && continue
            dir="$(dirname "$proj")"
            if [[ ! -f "$dir/README.md" ]]; then
              name="$(basename "$dir")"
              printf '# %s\n\nCI placeholder README.\n' "$name" > "$dir/README.md"
              echo "Created placeholder $dir/README.md"
            fi
          done < packlist.txt

      - name: Pack (by packlist)
        shell: bash
        run: |
          set -euo pipefail
          mkdir -p ./nupkg
          VER="${{ steps.vars.outputs.VER }}"
          while IFS= read -r proj; do
            [[ -z "${proj:-}" ]] && continue
            if [[ -n "${VER:-}" ]]; then
              echo "Packing $proj (Version=$VER)"
              dotnet pack "$proj" -c Release --no-build -o ./nupkg /p:Version="$VER"
            else
              echo "Packing $proj"
              dotnet pack "$proj" -c Release --no-build -o ./nupkg
            fi
          done < packlist.txt
          echo "Produced packages:" && ls -l ./nupkg

      - name: Upload nupkg
        uses: actions/upload-artifact@v4
        with:
          name: nupkg
          path: ./nupkg/*.nupkg

  release:
    needs: pack
    runs-on: ubuntu-latest
```
## Progesi/.github/workflows/semantic-pr.yml

```yaml
name: Semantic PR
on:
  pull_request_target:
    types: [opened, edited, synchronize, reopened, ready_for_review]

permissions:
  pull-requests: read
  statuses: write

jobs:
  lint:
    runs-on: ubuntu-latest
    steps:
      - uses: amannn/action-semantic-pull-request@v5
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        with:
          types: |
            chore
            ci
            docs
            feat
            fix
            perf
            refactor
            test
          requireScope: false
          subjectPattern: ^.+$
          wip: true
```
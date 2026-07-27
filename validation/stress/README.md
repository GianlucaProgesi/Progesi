# STRESS-1.1 validation assets

Opt-in robustness harness for Progesi core persistence (xUnit) and Rhino/Grasshopper (manual script).

## Part A — xUnit stress suite (`tests/Progesi.Stress.Tests`)

### Default CI / local test run (skipped)

Stress tests **do not run** unless explicitly enabled. Normal pipeline:

```powershell
dotnet build -c Release
dotnet test -c Release
```

Expected: all existing tests pass; stress tests report **Skipped** (not Failed).

### Opt-in stress run

```powershell
$env:PROGESI_STRESS = "1"
$env:PROGESI_STRESS_N = "2000"   # default 10000 when unset
dotnet test -c Release --filter "FullyQualifiedName~Stress"
```

Higher scale (plan target 100k):

```powershell
$env:PROGESI_STRESS = "1"
$env:PROGESI_STRESS_N = "100000"
dotnet test -c Release --filter "FullyQualifiedName~Stress"
```

### Environment variables

| Variable | Default | Purpose |
|----------|---------|---------|
| `PROGESI_STRESS` | (unset) | Must be `1` to run stress tests |
| `PROGESI_STRESS_N` | `10000` | Scale factor (variables, metadata rows, etc.) |

Timings are written to xUnit output (`ITestOutputHelper`); there are **no performance threshold assertions**.

## Part B — Grasshopper stress driver

See [GhStressDriver-CSharp.md](./GhStressDriver-CSharp.md) for the C# Script component source and wiring.

### Manual test matrix logging

After running the GH script on a scratch document, record results in the **Grasshopper Manual Test Matrix**:

| Field | Value |
|-------|-------|
| Test Area | Persistence / Regression |
| Suggested ID | GH-STRESS-001 |
| Component | C# Script — GhStressDriver |
| Preconditions | Progesi GH plug-in loaded; scratch `.3dm` |
| Steps | Paste script; `N=100`, `Run=true`; then optionally `N=1000` |
| Expected | `Ok=true`, report starts with `PASS`, timings present |
| Evidence | Paste report string + Rhino doc name + date |

## Scope

- **No production/Core changes** — tests and validation assets only.
- **No CI workflow edits** — skip gate is per-test via `Xunit.SkippableFact`.

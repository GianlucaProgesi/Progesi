Param(
  [string]$CiPath = ".github/workflows/ci.yml",
  [string]$Branch = "chore/ci-windows-x64-tests",
  [switch]$NoGit,            # se presente, non fa git add/commit/push
  [switch]$NoBranch          # se presente, non crea un nuovo branch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path $CiPath)) {
  Write-Error "File non trovato: $CiPath"
}

# 1) Backup
$backup = "$CiPath.bak"
Copy-Item $CiPath $backup -Force
Write-Host "Backup creato: $backup"

# 2) Carica contenuto
[string[]]$lines = Get-Content -Path $CiPath -Encoding UTF8
[string]$raw = [IO.File]::ReadAllText($CiPath)

# 3) Rileva matrice OS
$hasMatrixOs = $raw -match "(?ms)matrix:\s*\r?\n\s*os\s*:\s*\[?.+"

# 4) Se NON c'è matrice OS, converti runs-on di job test a windows-latest
# (micro-modifica: sostituisce solo ubuntu-latest/macOS con windows-latest)
if (-not $hasMatrixOs) {
  $lines = $lines | ForEach-Object {
    $ln = $_
    if ($ln -match "^\s*runs-on\s*:\s*(ubuntu-latest|ubuntu-.*|macos-latest|macos-.*)\s*$") {
      $indent = ($ln -replace "(runs-on.*)$","")
      "$indent" + "runs-on: windows-latest"
    } else {
      $ln
    }
  }
}

# 5) Aggiungi 'if: runner.os == Windows' allo step di Test se esiste una matrice OS
if ($hasMatrixOs) {
  $newLines = New-Object System.Collections.Generic.List[string]
  for ($i=0; $i -lt $lines.Count; $i++) {
    $newLines.Add($lines[$i])

    # Cerca uno step con - name: ...Test...
    if ($lines[$i] -match "^\s*-\s*name\s*:\s*(.+)$") {
      $nameVal = $Matches[1]
      if ($nameVal -match "(?i)test") {
        # Guarda la prossima riga significativa: se non è 'if:', inseriscila
        $j = $i + 1
        while ($j -lt $lines.Count -and ($lines[$j] -match "^\s*$")) { $j++ }
        $needIf = $true
        if ($j -lt $lines.Count -and $lines[$j] -match "^\s*if\s*:") { $needIf = $false }

        if ($needIf) {
          # Calcola indentazione: prendi quella della riga successiva, oppure quella di - name:
          $indent = ($lines[$i] -replace "(-\s*name.*)$","")
          $newLines.Add("$indent" + "if: runner.os == 'Windows'")
        }
      }
    }
  }
  $lines = $newLines.ToArray()
}

# 6) Inietta l’argomento x64 nei comandi 'dotnet test' (se manca)
$lines = $lines | ForEach-Object {
  $ln = $_
  if ($ln -match "^\s*run\s*:\s*dotnet\s+test\b" -and $ln -notmatch "RunConfiguration\.TargetPlatform\s*=\s*x64") {
    $ln + " -- RunConfiguration.TargetPlatform=x64"
  } else {
    $ln
  }
}

# 7) Salva file aggiornato
[IO.File]::WriteAllLines($CiPath, $lines, [Text.UTF8Encoding]::new($false))
Write-Host "Aggiornato: $CiPath"

# 8) Mostra diff (se git è disponibile)
try {
  git --version | Out-Null
  Write-Host ""
  Write-Host "Diff:"
  git --no-pager diff -- $CiPath
} catch {
  Write-Host "git non disponibile nel PATH, salto il diff."
}

# 9) Operazioni Git (branch, commit, push) se non disabilitate
if (-not $NoGit) {
  if (-not $NoBranch) {
    # Crea branch se non esiste già
    $currentBranch = (git rev-parse --abbrev-ref HEAD).Trim()
    if ($currentBranch -ne $Branch) {
      Write-Host "Creo/switch al branch: $Branch"
      git checkout -B $Branch
    } else {
      Write-Host "Branch corrente: $currentBranch"
    }
  }

  git add $CiPath
  git commit -m "CI: test .NET Framework su Windows x64 (+TargetPlatform=x64) - micro patch"
  # push con upstream se nuovo branch
  try {
    git push -u origin $Branch
  } catch {
    Write-Host "Push fallito. Verifica le credenziali o il remote."
  }
} else {
  Write-Host "Modalità NoGit: nessun commit/push eseguito."
}

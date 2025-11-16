param(
  [string]$Config = 'Release',
  [string]$Tf     = 'net48',
  # cartella librerie GH dell'utente
  [string]$GhLib  = "$env:APPDATA\Grasshopper\Libraries",
  [switch]$OpenTarget,
  [switch]$OnlyBuild
)

$ErrorActionPreference = 'Stop'

# === percorsi ===
$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$ToolProj = Join-Path $RepoRoot 'src\Progesi.EF.Tool\Progesi.EF.Tool.csproj'
$ToolBin  = Join-Path $RepoRoot "src\Progesi.EF.Tool\bin\$Config\$Tf"
$AsmBin   = Join-Path $RepoRoot "src\ProgesiGrasshopperAssembly\bin\$Config\$Tf"

if (-not (Test-Path $ToolProj)) { throw "Progetto non trovato: $ToolProj" }

# === aggiungi il progetto alla SLN se manca ===
$Sln = Get-ChildItem -Path $RepoRoot -Filter *.sln | Select-Object -First 1
if ($Sln) {
  $list = & dotnet sln $Sln.FullName list 2>$null
  if ($list -notmatch 'Progesi\.EF\.Tool') {
    Write-Host "Aggiungo Progesi.EF.Tool alla solution $($Sln.Name)…"
    & dotnet sln $Sln.FullName add $ToolProj | Out-Null
  }
}

# === build tool ===
Write-Host "Compilo: $ToolProj ($Config/$Tf)…"
& dotnet build $ToolProj -c $Config /nologo
if ($LASTEXITCODE -ne 0) { throw "Build tool fallita" }

if ($OnlyBuild) { Write-Host "[OK] Build completata"; exit 0 }

# === deploy: GH Libraries ===
if (-not (Test-Path $GhLib)) { New-Item -ItemType Directory -Path $GhLib | Out-Null }
Write-Host "Copia in GH Libraries → $GhLib"
Copy-Item -Path (Join-Path $ToolBin '*') -Destination $GhLib -Recurse -Force

# === deploy: accanto alla .gha ===
if (Test-Path $AsmBin) {
  Write-Host "Copia accanto alla .gha → $AsmBin"
  Copy-Item -Path (Join-Path $ToolBin '*') -Destination $AsmBin -Recurse -Force
} else {
  Write-Warning "Cartella .gha non trovata: $AsmBin (hai già compilato ProgesiGrasshopperAssembly?)"
}

if ($OpenTarget) { ii $GhLib }

# === smoke test rapido (crea un db con il tool) ===
$exe = Join-Path $GhLib 'Progesi.EF.Tool.exe'
if (Test-Path $exe) {
  $tmpDb = Join-Path $env:TEMP 'Progesi_EF_TOOL_SMOKE.db'
  if (Test-Path $tmpDb) { Remove-Item $tmpDb -Force }
  $psi = New-Object System.Diagnostics.ProcessStartInfo
  $psi.FileName = $exe
  $psi.Arguments = "export `"$tmpDb`""
  $psi.UseShellExecute = $false
  $psi.CreateNoWindow  = $true
  $psi.RedirectStandardOutput = $true
  $psi.RedirectStandardError  = $true
  $p = [System.Diagnostics.Process]::Start($psi)
  $out = $p.StandardOutput.ReadToEnd()
  $err = $p.StandardError.ReadToEnd()
  $p.WaitForExit()
  if ($p.ExitCode -eq 0 -and (Test-Path $tmpDb)) {
    Write-Host "[OK] EF tool operativo → $tmpDb"
    if ($out) { Write-Host $out.Trim() }
  } else {
    Write-Warning "EF tool test fallito (ExitCode=$($p.ExitCode))"
    if ($err) { Write-Warning $err.Trim() }
  }
} else {
  Write-Warning "Progesi.EF.Tool.exe non trovato in $GhLib"
}

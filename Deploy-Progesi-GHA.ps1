param(
  [string]$Config = "Release",
  [string]$Tf     = "net48",
  [string]$GhLib  = "$env:APPDATA\Grasshopper\Libraries",
  [switch]$OpenTarget
)

$ErrorActionPreference = "Stop"

# 1) percorsi di origine
$RepoRoot    = Split-Path -Parent $MyInvocation.MyCommand.Path
$AsmDir      = Join-Path $RepoRoot "src\ProgesiGrasshopperAssembly\bin\$Config\$Tf"

if (-not (Test-Path $AsmDir)) {
  throw "Cartella di build non trovata: $AsmDir (hai fatto 'dotnet build -c $Config'?)"
}
if (-not (Test-Path $GhLib)) {
  New-Item -ItemType Directory -Path $GhLib | Out-Null
}

# 2) log di deploy
$stamp   = Get-Date -Format "yyyyMMdd_HHmmss"
$LogFile = Join-Path $GhLib "Deploy_Progesi_$stamp.log"

"== Deploy Progesi ==", (Get-Date), "FROM: $AsmDir", "TO:   $GhLib", "" | Out-File $LogFile -Encoding UTF8

# 3) copia ricorsiva di tutto il contenuto (gha, dll, pdb, subfolder)
#    -Exclude per file temporanei comuni
$exclude = @("*.tmp","*.TMP","*.vshost.*","*.log")
Copy-Item -Path (Join-Path $AsmDir "*") -Destination $GhLib -Recurse -Force -Exclude $exclude

# 4) verifica presenza della .gha
$gha = Get-ChildItem -Path $GhLib -Filter "ProgesiGrasshopperAssembly.gha" -Recurse | Select-Object -First 1
if (-not $gha) {
  Add-Content $LogFile "ATTENZIONE: .gha non trovata in $GhLib (deploy parziale?)."
  Write-Warning "ATTENZIONE: .gha non trovata in $GhLib (deploy parziale?)"
} else {
  Add-Content $LogFile "OK: trovato $($gha.FullName)"
}

# 5) opzionale: apri target in Explorer
if ($OpenTarget) { ii $GhLib }

Write-Host "[OK] Deploy completato → $GhLib"
Write-Host "Log: $LogFile"

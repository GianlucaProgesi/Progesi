param(
  [string]$Path = "thirdparty\Rhino8.20",
  [switch]$Commit
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$full = Join-Path $root $Path

if (-not (Test-Path $full)) { throw "Cartella non trovata: $full" }

# 1) .gitattributes per tracciare i binari (lascia LF come sono)
$attr = Join-Path $root ".gitattributes"
$attrContent = @"
# mantieni i binari di Rhino nella repo
$Path/*.dll  -text
"@
if (-not (Test-Path $attr)) { Set-Content -Path $attr -Value $attrContent -Encoding ASCII }
else {
  $cur = Get-Content $attr -Raw
  if ($cur -notmatch [regex]::Escape("$Path/*.dll")) {
    Add-Content -Path $attr -Value "`n$Path/*.dll  -text"
  }
}

# 2) .gitignore: NON ignorare thirdparty (rimuovi eventuale regola)
$ign = Join-Path $root ".gitignore"
if (Test-Path $ign) {
  $gi = Get-Content $ign
  $gi = $gi | Where-Object { $_ -notmatch '^[\/\\]?thirdparty\/?' }
  Set-Content -Path $ign -Value ($gi -join "`n") -Encoding ASCII
}

# 3) placeholder .keep (per mantenere la cartella in repo anche se svuotata)
$keep = Join-Path $full ".keep"
if (-not (Test-Path $keep)) { Set-Content -Path $keep -Value "" -Encoding ASCII }

Write-Host "[OK] thirdparty protetta in git: $Path"

if ($Commit) {
  & git add $attr $ign $keep $full
  & git commit -m "chore(thirdparty): add Rhino 8.20 SDK and protect folder in git"
  if ($LASTEXITCODE -ne 0) { Write-Warning "Nessuna modifica da committare (ok)" }
}

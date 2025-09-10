# tools/Update-Readme-Hero.ps1
[CmdletBinding()]
param(
  [string]$ReadmePath = "./README.md",
  [string]$LogoSourcePath = "",
  [string]$Owner = "GianlucaProgesi",
  [string]$Repo  = "Progesi",
  [string]$NuGetPackage = "ProgesiCore",
  [string]$HeroTitle = "Progesi Engineering Toolchain"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Write-FileUtf8([string]$Path, [string]$Text) {
  $dir = Split-Path -Parent $Path
  if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
  $enc = New-Object System.Text.UTF8Encoding($false) # no BOM
  [System.IO.File]::WriteAllText($Path, $Text, $enc)
}

function Insert-Or-Replace {
  param(
    [Parameter(Mandatory=$true)][string]$Content,
    [Parameter(Mandatory=$true)][string]$StartMarker,
    [Parameter(Mandatory=$true)][string]$EndMarker,
    [Parameter(Mandatory=$true)][string]$Payload
  )
  $managed = $StartMarker + "`n" + $Payload.TrimEnd() + "`n" + $EndMarker
  if ($Content -match [regex]::Escape($StartMarker)) {
    $pattern = [regex]::Escape($StartMarker) + "(?s).*?" + [regex]::Escape($EndMarker)
    return [regex]::Replace($Content, $pattern, $managed)
  } else {
    return ($managed + "`n`n" + $Content.TrimStart())
  }
}

# 1) Logo
$logoTarget = "docs/assets/progesi-logo.jpg"
if ($LogoSourcePath) {
  if (-not (Test-Path -LiteralPath $LogoSourcePath)) {
    Write-Warning "LogoSourcePath non trovato: $LogoSourcePath (procedo senza copiare)."
  } else {
    $logoDir = Split-Path -Parent $logoTarget
    if (-not (Test-Path -LiteralPath $logoDir)) { New-Item -ItemType Directory -Path $logoDir -Force | Out-Null }
    Copy-Item -LiteralPath $LogoSourcePath -Destination $logoTarget -Force
    Write-Host "✓ Logo copiato in $logoTarget"
  }
} elseif (-not (Test-Path -LiteralPath $logoTarget)) {
  Write-Warning "Logo non trovato in '$logoTarget'. Usa -LogoSourcePath per copiarlo automaticamente."
}

# 2) HERO block (tutto con doppi apici)
$buildBadge = "https://github.com/$Owner/$Repo/actions/workflows/release.yml/badge.svg"
$buildLink  = "https://github.com/$Owner/$Repo/actions/workflows/release.yml"
$nugetVer   = "https://img.shields.io/nuget/v/$NuGetPackage.svg"
$nugetDls   = "https://img.shields.io/nuget/dt/$NuGetPackage.svg"
$nugetLink  = "https://www.nuget.org/packages/$NuGetPackage"
$healthBadge= "https://img.shields.io/badge/Release%20Health-Run%20check-2ea44f?logo=powershell&logoColor=white"

$heroLines = @(
  "<p align=""center"">",
  "  <img src=""$logoTarget"" alt=""Progesi Logo"" width=""400""/>",
  "</p>",
  "",
  "<h1 align=""center"">$HeroTitle</h1>",
  "",
  "<p align=""center"">",
  "  <a href=""$buildLink""><img src=""$buildBadge"" alt=""Build""/></a>",
  "  <a href=""$nugetLink""><img src=""$nugetVer"" alt=""NuGet""/></a>",
  "  <a href=""$nugetLink""><img src=""$nugetDls"" alt=""Downloads""/></a>",
  "  <a href=""tools/Release-HealthCheck.ps1""><img src=""$healthBadge"" alt=""Release Health""/></a>",
  "</p>",
  "",
  "---"
)

# 3) Sezione overview/features inclusa se non già gestita da marker
$content = if (Test-Path -LiteralPath $ReadmePath) { Get-Content -LiteralPath $ReadmePath -Raw } else { "# Progesi`n" }
$hasManagedOverview = ($content -match "<!--\s*PROGESI:OVERVIEW:START\s*-->")

if (-not $hasManagedOverview) {
  $overviewAndFeatures = @(
    "",
    "## ℹ️ Overview",
    "",
    "**Progesi** – a modular toolchain for bridge and structural engineering:",
    "- 🧩 **Grasshopper/Rhino components** for variables, metadata, and repositories",
    "- 📦 Modular **NuGet packages** with SourceLink and built-in docs",
    "- 🚀 Automated **CI/CD pipeline** (NuGet.org + GitHub Packages)",
    "- 📝 Auto-generated **CHANGELOG** and **README** via PowerShell scripts",
    "- ✅ **Health check** and maintenance checklist for reliable releases",
    "",
    "---",
    "",
    "**Progesi** è una toolchain modulare per l’ingegneria dei ponti e delle strutture complesse:",
    "- 🧩 Componenti **Grasshopper/Rhino** per variabili, metadata e repository",
    "- 📦 Pacchetti **NuGet** modulari con SourceLink e documentazione integrata",
    "- 🚀 Pipeline **CI/CD** automatizzata (NuGet.org + GitHub Packages)",
    "- 📝 **CHANGELOG** e **README** generati automaticamente via script PowerShell",
    "- ✅ **Health check** e checklist di manutenzione per rilasci affidabili",
    "",
    "---",
    "",
    "## ✨ Features",
    "",
    "- ⚡ **Parametric Power** – define, manage and reuse structural variables",
    "- 🗂️ **Repositories Everywhere** – Rhino, SQLite, InMemory, unified",
    "- 🔄 **CI/CD Ready** – seamless NuGet + GitHub Packages integration",
    "- 📊 **Transparent Development** – automated changelog & docs",
    "- 🛡️ **Reliable Releases** – built-in health check & maintenance flow",
    "",
    "---"
  )
  $heroLines += $overviewAndFeatures
}

$heroBlock = ($heroLines -join "`n")
$heroStart = "<!-- PROGESI:HERO:START -->"
$heroEnd   = "<!-- PROGESI:HERO:END -->"

# 4) Applica HERO in cima
$out = Insert-Or-Replace -Content $content -StartMarker $heroStart -EndMarker $heroEnd -Payload $heroBlock
$out = $out -replace "`r`n","`n"
Write-FileUtf8 -Path $ReadmePath -Text $out

Write-Host "README aggiornato con blocco HERO (logo, titolo, badge)" -ForegroundColor Green

if (-not (Test-Path -LiteralPath $logoTarget)) {
  Write-Host ""
  Write-Warning "Logo non presente in '$logoTarget'. Ripeti con -LogoSourcePath 'C:\\path\\al\\logo.jpg' per copiarlo."
}

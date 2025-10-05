<# Set-Version.ps1
USO:
  .\tools\Set-Version.ps1 -Version 0.8.0 [-Changelog "Breve descrizione"]
#>

[CmdletBinding()] param(
  [Parameter(Mandatory=$true)] [string]$Version,
  [string]$Changelog = ""
)
$ErrorActionPreference='Stop'

$project = "src\ProgesiGrasshopperAssembly"
$asmInfo = Join-Path $project "Properties\AssemblyInfo.cs"
if (!(Test-Path $asmInfo)) { throw "AssemblyInfo non trovato: $asmInfo" }

# aggiorna [assembly: AssemblyVersion] e [assembly: AssemblyFileVersion]
$content = Get-Content $asmInfo -Raw
$content = $content -replace '(?m)^\s*\[assembly:\s*AssemblyVersion\(".*?"\)\s*\]\s*$', "[assembly: AssemblyVersion(`"$Version`")]"
$content = $content -replace '(?m)^\s*\[assembly:\s*AssemblyFileVersion\(".*?"\)\s*\]\s*$', "[assembly: AssemblyFileVersion(`"$Version`")]"
Set-Content -Path $asmInfo -Value $content -Encoding UTF8

# VERSION.txt alla radice repo
"Version: $Version`nBuild: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')`n" | Set-Content -Path ".\VERSION.txt" -Encoding UTF8

# CHANGELOG.md append
if ($Changelog -and (Test-Path ".\CHANGELOG.md")) {
  "`n## $Version - $(Get-Date -Format yyyy-MM-dd)`n- $Changelog`n" | Add-Content ".\CHANGELOG.md" -Encoding UTF8
} elseif ($Changelog) {
  "## $Version - $(Get-Date -Format yyyy-MM-dd)`n- $Changelog`n" | Set-Content ".\CHANGELOG.md" -Encoding UTF8
}

Write-Host "Versione aggiornata a $Version" -ForegroundColor Green

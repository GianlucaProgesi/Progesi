[CmdletBinding()]
param(
  [string]$RepoRoot = "."
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$propsPath = Join-Path $RepoRoot 'Directory.Build.props'

# Contenuto consigliato per MinVer (tag: vX.Y.Z)
$propsXml = @'
<Project>
  <PropertyGroup>
    <MinVerTagPrefix>v</MinVerTagPrefix>
    <MinVerDefaultPreReleasePhase></MinVerDefaultPreReleasePhase>
    <MinVerAutoIncrement>patch</MinVerAutoIncrement>
    <MinVerBuildMetadata>build.{0:yyyyMMdd-HHmmss}</MinVerBuildMetadata>
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MinVer" Version="5.0.0" PrivateAssets="All" />
  </ItemGroup>
</Project>
'@

# Scrivi/aggiorna props (semplice sovrascrittura – idempotente per il nostro scopo)
Set-Content -LiteralPath $propsPath -Value $propsXml -Encoding UTF8
Write-Host "✓ Directory.Build.props configurato per MinVer"

# Patch workflow: rimuovi /p:Version e step Compute version
$wf = '.github/workflows/release.yml'
if (Test-Path $wf) {
  $yml = Get-Content $wf -Raw
  # Rimuovi step 'Compute version (if tag)' e simili
  $yml = $yml -replace '(?ms)^\s*-\s*name:\s*Compute version.*?(?=^\s*-\s*name:|\Z)', ''
  # Rimuovi riferimenti ${{ steps.vars.outputs.VER }}
  $yml = $yml -replace '\.\$\{\{\s*steps\.vars\.outputs\.VER\s*\}\}', ''
  $yml = $yml -replace '\*\.\$\{\{\s*steps\.vars\.outputs\.VER\s*\}\}\.nupkg', '*.nupkg'
  # Rimuovi eventuale /p:Version=...
  $yml = $yml -replace '/p:Version="\$\{\{.*?\}\}"', ''
  Set-Content $wf $yml -Encoding UTF8
  Write-Host "✓ Workflow release.yml aggiornato per MinVer"
} else {
  Write-Host "(!) release.yml non trovato: salta patch" -ForegroundColor Yellow
}

Write-Host "`nFatto: versione determinata automaticamente dai tag vX.Y.Z" -ForegroundColor Green

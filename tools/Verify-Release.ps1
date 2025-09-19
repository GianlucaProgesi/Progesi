[CmdletBinding()]
param(
  [string]$Version,                 # es: 1.2.3 o 1.2.3-beta.1
  [switch]$PackIfMissing,           # se non trova i nupkg, fa dotnet pack
  [switch]$PublishGpr,              # prova a pubblicare su GitHub Packages
  [string]$GprToken,                # PAT o token; se vuoto e in GH Actions usa GITHUB_TOKEN
  [string]$Owner = $env:GITHUB_REPOSITORY_OWNER
)

$ErrorActionPreference = "Stop"
if (-not $Owner -or $Owner -eq "") { $Owner = "GianlucaProgesi" }

if (-not $Version) {
  try {
    $tag = git describe --tags --abbrev=0 2>$null
    if ($tag -and $tag.StartsWith("v")) { $Version = $tag.Substring(1) }
  } catch {}
}
if (-not $Version) { throw "Specify -Version (es: 1.0.0) or tag the repo (vX.Y.Z)" }

Write-Host "== Verify Release for version $Version (owner=$Owner)" -ForegroundColor Cyan

$nupkg = Get-ChildItem -Recurse -Filter "*.$Version.nupkg" -ErrorAction SilentlyContinue
if (-not $nupkg -and $PackIfMissing) {
  Write-Host "No nupkg found for .$Version — packing ..." -ForegroundColor Yellow
  $projects = Get-ChildItem ./src -Recurse -Filter *.csproj | Where-Object { $_.FullName -notmatch 'ProgesiGrasshopperAssembly' }
  New-Item -ItemType Directory -Force -Path nupkg | Out-Null
  foreach ($p in $projects) {
    & dotnet pack $p.FullName -c Release --no-build -o ./nupkg /p:Version=$Version
  }
  $nupkg = Get-ChildItem -Recurse -Filter "*.$Version.nupkg"
}

if (-not $nupkg) { throw "No packages matching *.$Version.nupkg" }
Write-Host "Packages:" -ForegroundColor Green
$nupkg | ForEach-Object { Write-Host (" - {0}" -f $_.Name) }

if ($PublishGpr) {
  $tok = if ($GprToken) { $GprToken } elseif ($env:GITHUB_TOKEN) { $env:GITHUB_TOKEN } else { "" }
  if (-not $tok) { throw "No token provided. Use -GprToken <PAT> or set GITHUB_TOKEN." }

  dotnet nuget remove source github 2>$null | Out-Null
  dotnet nuget add source "https://nuget.pkg.github.com/$Owner/index.json" `
    --name "github" --username "$Owner" --password "$tok" --store-password-in-clear-text | Out-Null

  foreach ($pkg in $nupkg) {
    Write-Host ("Pushing {0} to GPR..." -f $pkg.Name) -ForegroundColor Cyan
    & dotnet nuget push $pkg.FullName --source github --api-key $tok --skip-duplicate
  }
  Write-Host "Publish completed."
} else {
  Write-Host "Dry-run complete. Use -PublishGpr -GprToken <token> to push to GPR." -ForegroundColor DarkYellow
}

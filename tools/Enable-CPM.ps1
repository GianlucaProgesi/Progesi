Param(
  [string]$BranchName = "chore/cpm-versions",
  [string]$Solution = "Progesi.sln"
)

$ErrorActionPreference = "Stop"
function Info($m){ Write-Host $m -ForegroundColor Cyan }
function Ok($m){ Write-Host $m -ForegroundColor Green }
function Warn($m){ Write-Warning $m }
function Fail($m){ Write-Host $m -ForegroundColor Red }

# 0) prerequisiti
git rev-parse --is-inside-work-tree *> $null
if ($LASTEXITCODE -ne 0){ Fail "Non sei in una repo Git"; exit 1 }

# 1) crea branch di lavoro (se sei su main o su altro branch)
$cur = (git rev-parse --abbrev-ref HEAD | Out-String).Trim()
if ($cur -eq "main") {
  Info ("Creo branch $BranchName")
  git checkout -b $BranchName | Out-Null
} else {
  Info ("Userò il branch corrente: $cur (se vuoi un branch dedicato, passalo con -BranchName)")
  $BranchName = $cur
}

# 2) Directory.Packages.props (CPM attivo)
$props = @'
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Microsoft.Data.Sqlite" Version="9.0.9" />
    <PackageVersion Include="SQLitePCLRaw.bundle_e_sqlite3" Version="2.1.10" />
    <PackageVersion Include="SQLitePCLRaw.provider.e_sqlite3" Version="2.1.10" />
    <PackageVersion Include="SQLitePCLRaw.core" Version="2.1.10" />
  </ItemGroup>
</Project>
'@
Set-Content -Path ".\Directory.Packages.props" -Encoding UTF8 -Value $props

# 3) Rimuovi Version="..." nei csproj per i pacchetti pinnati
$targets = @(
  "Microsoft.Data.Sqlite",
  "SQLitePCLRaw.bundle_e_sqlite3",
  "SQLitePCLRaw.provider.e_sqlite3",
  "SQLitePCLRaw.core"
)
$changed = @()
Get-ChildItem -Path src,tests,tools -Recurse -Filter *.csproj -ErrorAction SilentlyContinue | ForEach-Object {
  $file = $_.FullName
  $text = Get-Content $file -Raw
  $orig = $text
  foreach($pkg in $targets){
    # rimuove Version="..." solo dalla riga con Include="<pkg>"
    $text = $text -replace "(<PackageReference[^>]*Include\s*=\s*`"$([regex]::Escape($pkg))`"[^>]*?)\s+Version\s*=\s*`"[^`"]+`"", '$1'
  }
  if ($text -ne $orig) {
    Set-Content $file -Encoding UTF8 -Value $text
    $changed += $file
  }
}

# 4) Restore / Build / Test
Info "dotnet restore..."
dotnet restore $Solution --nologo
Info "dotnet build (Release)..."
dotnet build $Solution -c Release --nologo --no-restore
Info "dotnet test (Release, no-build)..."
dotnet test  $Solution -c Release --no-build --nologo

# 5) Commit + push + PR
git add Directory.Packages.props
if ($changed.Count -gt 0) { git add $changed }
git commit -m "ci: pin packages via Central Package Management (Sqlite/SQLitePCLRaw)"
git push -u origin $BranchName

# crea PR
try {
  $prUrl = gh pr create --base main --head $BranchName `
    --title "ci: pin packages (Central Package Management)" `
    --body "Pinna Microsoft.Data.Sqlite=9.0.9 e SQLitePCLRaw=2.1.10 con CPM. Build/test verdi."
  Ok ("PR creata: {0}" -f $prUrl)
} catch {
  Warn "PR forse già esistente: visualizzo"
  gh pr view --head $BranchName --web
}

Ok "Fatto."
if ($changed.Count -gt 0) { Write-Host "Modificati:" -ForegroundColor Yellow; $changed | ForEach-Object { "  * $_" | Write-Host } }

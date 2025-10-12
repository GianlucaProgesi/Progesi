Param(
  [string]$Solution = "Progesi.sln",
  [string]$BranchName = "chore/cpm-versions",
  [switch]$NoCommit  # se vuoi solo provare senza commit/push
)

$ErrorActionPreference = "Stop"
function Info($m){ Write-Host $m -ForegroundColor Cyan }
function Ok($m){ Write-Host $m -ForegroundColor Green }
function Warn($m){ Write-Warning $m }
function Fail($m){ Write-Host $m -ForegroundColor Red }

# 0) git sanity
git rev-parse --is-inside-work-tree *> $null
if ($LASTEXITCODE -ne 0){ Fail "Non sei in una repo Git"; exit 1 }

# 1) branch di lavoro (se sei su main, spostati)
$cur = (git rev-parse --abbrev-ref HEAD | Out-String).Trim()
if ($cur -eq "main") { git checkout -b $BranchName | Out-Null }

# 2) scansiona tutti i csproj in src/tests/tools
$csprojs = Get-ChildItem -Path src, tests, tools -Recurse -Filter *.csproj -ErrorAction SilentlyContinue
if (-not $csprojs) { Fail "Nessun .csproj trovato"; exit 1 }

# 3) raccogli tutte le <PackageReference Include="..." Version="..."> o con <Version>...</Version>
$map = @{}  # nome pacchetto -> versione (stringa)
foreach($p in $csprojs){
  try{
    [xml]$xml = Get-Content $p.FullName
  }catch{
    Warn "XML non leggibile: $($p.FullName)"; continue
  }
  $refs = $xml.Project.ItemGroup.PackageReference
  if (-not $refs) { continue }

  foreach($r in $refs){
    $name = $r.Include
    if (-not $name) { continue }
    # prendi versione da attr o child
    $v = $null
    if ($r.Version) { $v = $r.Version } 
    else {
      # child <Version> come nodo
      $vn = $r.ChildNodes | Where-Object { $_.Name -eq "Version" } | Select-Object -First 1
      if ($vn) { $v = $vn.InnerText }
    }
    if ($v) {
      # se lo stesso package compare con versioni diverse, tieni la "maggiore" in senso stringa
      if (-not $map.ContainsKey($name)) { $map[$name] = $v }
      else {
        # se entrambe sono semver prova a tenere la maggiore, altrimenti preferisci quella più lunga (più specifica)
        $old = $map[$name]
        if ($v -match '^\d' -and $old -match '^\d') {
          try {
            $verNew = [Version]($v -replace '[^\d\.].*$','')
            $verOld = [Version]($old -replace '[^\d\.].*$','')
            if ($verNew -gt $verOld) { $map[$name] = $v }
          } catch {
            if ($v.Length -gt $old.Length) { $map[$name] = $v }
          }
        } else {
          if ($v.Length -gt $old.Length) { $map[$name] = $v }
        }
      }
    }
  }
}

if ($map.Count -eq 0) { Fail "Nessun PackageReference con Version trovato"; exit 1 }

# 4) scrivi/aggiorna Directory.Packages.props con TUTTE le versioni trovate
$propsPath = Join-Path (Get-Location) "Directory.Packages.props"
$sb = New-Object System.Text.StringBuilder
$null = $sb.AppendLine('<Project>')
$null = $sb.AppendLine('  <PropertyGroup>')
$null = $sb.AppendLine('    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>')
$null = $sb.AppendLine('  </PropertyGroup>')
$null = $sb.AppendLine('  <ItemGroup>')
foreach($k in ($map.Keys | Sort-Object)){
  $line = ('    <PackageVersion Include="{0}" Version="{1}" />' -f $k, $map[$k])
  $null = $sb.AppendLine($line)
}
$null = $sb.AppendLine('  </ItemGroup>')
$null = $sb.AppendLine('</Project>')
Set-Content -Path $propsPath -Encoding UTF8 -Value $sb.ToString()

# 5) rimuovi le Version dai csproj (attr e child) per i pacchetti ora centralizzati
$changed = @()
foreach($p in $csprojs){
  [xml]$xml = Get-Content $p.FullName
  $dirty = $false
  $refs = $xml.Project.ItemGroup.PackageReference
  foreach($r in $refs){
    $name = $r.Include
    if (-not $name) { continue }
    if (-not $map.ContainsKey($name)) { continue } # non toccare pacchetti non centralizzati
    # rimuovi attrib Version
    if ($r.Version){
      $r.RemoveAttribute("Version"); $dirty = $true
    }
    # rimuovi child <Version>...</Version>
    $vn = $r.ChildNodes | Where-Object { $_.Name -eq "Version" } | Select-Object -First 1
    if ($vn) {
      $r.RemoveChild($vn) | Out-Null; $dirty = $true
    }
  }
  if ($dirty) {
    $xml.Save($p.FullName)
    $changed += $p.FullName
  }
}

# 6) restore/build/test
Info "dotnet restore..."
dotnet restore $Solution --nologo
Info "dotnet build (Release)..."
dotnet build $Solution -c Release --nologo --no-restore
Info "dotnet test (Release, no-build)..."
dotnet test  $Solution -c Release --no-build --nologo

# 7) commit + push
if (-not $NoCommit){
  git add $propsPath
  if ($changed.Count -gt 0) { git add $changed }
  git commit -m "ci(cpm): centralize all package versions (auto-generated)"
  git push -u origin $BranchName
}
Ok "CPM applicato e build/test verdi."

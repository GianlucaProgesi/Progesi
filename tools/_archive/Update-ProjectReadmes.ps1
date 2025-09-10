[CmdletBinding()]
param(
  [string]$Root = "./src",
  [string]$Owner = "GianlucaProgesi"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Get-PackageId {
  param([string]$CsprojPath)
  [xml]$xml = Get-Content -LiteralPath $CsprojPath
  $pg = $xml.Project.PropertyGroup | Select-Object -First 1
  if (-not $pg) { return [System.IO.Path]::GetFileNameWithoutExtension($CsprojPath) }

  $pkgNode = $pg.SelectSingleNode('PackageId')
  if ($pkgNode -and -not [string]::IsNullOrWhiteSpace($pkgNode.InnerText)) {
    return $pkgNode.InnerText
  }
  $asmNode = $pg.SelectSingleNode('AssemblyName')
  if ($asmNode -and -not [string]::IsNullOrWhiteSpace($asmNode.InnerText)) {
    return $asmNode.InnerText
  }
  return [System.IO.Path]::GetFileNameWithoutExtension($CsprojPath)
}

function Ensure-PackageReadmeFile {
  param([string]$CsprojPath)
  [xml]$xml = Get-Content -LiteralPath $CsprojPath
  $proj = $xml.Project
  $pg = $proj.PropertyGroup | Select-Object -First 1
  if (-not $pg) { $pg = $xml.CreateElement('PropertyGroup'); $null = $proj.AppendChild($pg) }

  $node = $pg.SelectSingleNode('PackageReadmeFile')
  $changed = $false
  if ($node) {
    if ($node.InnerText -ne 'README.md') { $node.InnerText = 'README.md'; $changed = $true }
  } else {
    $node = $xml.CreateElement('PackageReadmeFile')
    $node.InnerText = 'README.md'
    $null = $pg.AppendChild($node)
    $changed = $true
  }

  if ($changed) { $xml.Save($CsprojPath) }
}

function New-HeaderAndBadges {
  param([string]$Display,[string]$PackageId,[string]$Owner)
  $lines = @()
  $lines += "# $Display"
  $lines += ""
  $lines += ("[![NuGet {0}](https://img.shields.io/nuget/v/{1}.svg)](https://www.nuget.org/packages/{1})" -f $Display,$PackageId)
  $lines += ("![Downloads {0}](https://img.shields.io/nuget/dt/{1})" -f $Display,$PackageId)
  $lines += ""
  return ,$lines
}

function New-CoreBody {
  param([string]$PackageId)
  $l = @()
  $l += 'Core types for Progesi.'
  $l += ''
  $l += '## Install'
  $l += ('`dotnet add package {0}`' -f $PackageId)
  $l += ''
  $l += '## Quick start'
  $l += '```csharp'
  $l += 'using ProgesiCore;'
  $l += ''
  $l += 'var span = new ProgesiVariable("Span", 35.0);'
  $l += 'Console.WriteLine($"{span.Name} = {span.Value}");'
  $l += '```'
  $l += ''
  $l += '## Links'
  $l += '- NuGet: https://www.nuget.org/packages/ProgesiCore'
  return ,$l
}

function New-InMemoryBody {
  param([string]$PackageId)
  $l = @()
  $l += 'In-memory repository implementation for Progesi. Great for tests and prototypes.'
  $l += ''
  $l += '## Install'
  $l += ('`dotnet add package {0}`' -f $PackageId)
  $l += ''
  $l += '## Quick start'
  $l += '```csharp'
  $l += 'using ProgesiCore;'
  $l += 'using ProgesiRepositories.InMemory;'
  $l += ''
  $l += 'var repo = new InMemoryVariablesRepository();'
  $l += 'repo.Save(new ProgesiVariable("Span", 35.0));'
  $l += 'var v = repo.Get("Span");'
  $l += 'Console.WriteLine(v.Value);'
  $l += '```'
  $l += ''
  $l += '## Links'
  $l += '- NuGet: https://www.nuget.org/packages/ProgesiRepositories.InMemory'
  return ,$l
}

function New-RhinoBody {
  param([string]$PackageId)
  $l = @()
  $l += 'Rhino-based repository (integration with Rhino/Grasshopper data).'
  $l += ''
  $l += '## Install'
  $l += ('`dotnet add package {0}`' -f $PackageId)
  $l += ''
  $l += '## Quick note'
  $l += '- Requires Rhino environment for runtime integration.'
  $l += '- Use together with ProgesiCore.'
  $l += ''
  $l += '## Example (conceptual)'
  $l += '```csharp'
  $l += 'using ProgesiCore;'
  $l += 'using ProgesiRepositories.Rhino;'
  $l += ''
  $l += '// var repo = new RhinoVariablesRepository(doc);'
  $l += '// repo.Save(new ProgesiVariable("Span", 35.0));'
  $l += '```'
  $l += ''
  $l += '## Links'
  $l += '- NuGet: https://www.nuget.org/packages/ProgesiRepositories.Rhino'
  return ,$l
}

function New-SqliteBody {
  param([string]$PackageId)
  $l = @()
  $l += 'SQLite-backed repository for Progesi.'
  $l += ''
  $l += '## Install'
  $l += ('`dotnet add package {0}`' -f $PackageId)
  $l += ''
  $l += '## Quick start'
  $l += '```csharp'
  $l += 'using ProgesiCore;'
  $l += 'using ProgesiRepositories.Sqlite;'
  $l += ''
  $l += 'var path = System.IO.Path.Combine(Environment.CurrentDirectory, "progesi.db");'
  $l += 'var repo = new SqliteVariablesRepository($"Data Source={path}");'
  $l += 'repo.Save(new ProgesiVariable("Span", 35.0));'
  $l += 'var v = repo.Get("Span");'
  $l += 'Console.WriteLine(v.Value);'
  $l += '```'
  $l += ''
  $l += '## Links'
  $l += '- NuGet: https://www.nuget.org/packages/ProgesiRepositories.Sqlite'
  return ,$l
}

function New-GenericBody {
  param([string]$PackageId,[string]$Display)
  $l = @()
  $l += 'NuGet package for Progesi.'
  $l += ''
  $l += '## Install'
  $l += ('`dotnet add package {0}`' -f $PackageId)
  $l += ''
  $l += '## Links'
  $l += ("- NuGet: https://www.nuget.org/packages/{0}" -f $PackageId)
  return ,$l
}

function Build-Content {
  param([string]$Display,[string]$PackageId,[string]$Owner)
  $top = New-HeaderAndBadges -Display $Display -PackageId $PackageId -Owner $Owner

  $id = $PackageId.ToLowerInvariant()
  if     ($id -eq 'progesicore')                  { $body = New-CoreBody      -PackageId $PackageId }
  elseif ($id -eq 'progesirepositories.inmemory') { $body = New-InMemoryBody  -PackageId $PackageId }
  elseif ($id -eq 'progesirepositories.rhino')    { $body = New-RhinoBody     -PackageId $PackageId }
  elseif ($id -eq 'progesirepositories.sqlite')   { $body = New-SqliteBody    -PackageId $PackageId }
  else                                            { $body = New-GenericBody   -PackageId $PackageId -Display $Display }

  $managed = @()
  $managed += '<!-- PROGESI:BODY:START -->'
  $managed += $body
  $managed += '<!-- PROGESI:BODY:END -->'

  return ,($top + '' + $managed)
}

function Set-ManagedBody {
  param([string]$Existing,[string]$NewBodyBlock)
  $start = '<!-- PROGESI:BODY:START -->'
  $end   = '<!-- PROGESI:BODY:END -->'

  if ([string]::IsNullOrWhiteSpace($Existing)) { return $NewBodyBlock -join "`n" }

  if ($Existing -match [regex]::Escape($start)) {
    $pattern = [regex]::Escape($start) + '(?s).*?' + [regex]::Escape($end)
    return [regex]::Replace($Existing, $pattern, ($NewBodyBlock -join "`n"))
  }

  if ($Existing -match 'CI placeholder README') { return $NewBodyBlock -join "`n" }

  return (($NewBodyBlock -join "`n") + "`n`n" + $Existing)
}

$csprojs = Get-ChildItem -Path $Root -Recurse -Filter *.csproj | Sort-Object FullName
foreach ($cs in $csprojs) {
  $projDir = Split-Path -Parent $cs.FullName
  $pkgId   = Get-PackageId -CsprojPath $cs.FullName
  $display = [System.IO.Path]::GetFileNameWithoutExtension($cs.FullName)

  Ensure-PackageReadmeFile -CsprojPath $cs.FullName

  $target = Join-Path $projDir 'README.md'
  $newBlock = Build-Content -Display $display -PackageId $pkgId -Owner $Owner

  if (Test-Path $target) {
    $existing = Get-Content -LiteralPath $target -Raw
    $updated  = Set-ManagedBody -Existing $existing -NewBodyBlock $newBlock
    $updated  = $updated -replace "`r`n","`n"
    Set-Content -LiteralPath $target -Value $updated -Encoding UTF8
    Write-Host ("Updated {0}" -f $target)
  } else {
    $content = $newBlock -join "`n"
    Set-Content -LiteralPath $target -Value ($content -replace "`r`n","`n") -Encoding UTF8
    Write-Host ("Created {0}" -f $target)
  }
}

Write-Host "`nDone: project READMEs generated/updated." -ForegroundColor Green

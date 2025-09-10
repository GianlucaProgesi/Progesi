[CmdletBinding()]
param(
  [string]$Owner = 'GianlucaProgesi',
  [string]$Repo  = 'Progesi',
  [string]$WorkflowFile = 'release.yml',
  [string]$ReadmePath = './README.md',
  [hashtable[]]$Packages = @(
    @{ Id = 'ProgesiCore';                  Display = 'ProgesiCore' },
    @{ Id = 'ProgesiRepositories.InMemory'; Display = 'ProgesiRepositories.InMemory' },
    @{ Id = 'ProgesiRepositories.Rhino';    Display = 'ProgesiRepositories.Rhino' },
    @{ Id = 'ProgesiRepositories.Sqlite';   Display = 'ProgesiRepositories.Sqlite' }
  ),
  [bool]$IncludeGprSection = $true
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function New-BadgeBlock {
  param([string]$Owner,[string]$Repo,[string]$WorkflowFile,[hashtable[]]$Packages)
  $nl = [Environment]::NewLine
  $lines = @()
  $lines += ('[![Build](https://github.com/{0}/{1}/actions/workflows/{2}/badge.svg)](https://github.com/{0}/{1}/actions/workflows/{2})' -f $Owner,$Repo,$WorkflowFile)
  $lines += ''
  foreach ($p in $Packages) { $lines += ('[![NuGet {0}](https://img.shields.io/nuget/v/{1}.svg)](https://www.nuget.org/packages/{1})' -f $p.Display,$p.Id) }
  $lines += ''
  foreach ($p in $Packages) { $lines += ('![Downloads {0}](https://img.shields.io/nuget/dt/{1})' -f $p.Display,$p.Id) }
  return ($lines -join $nl)
}

function New-PackagesTableBlock {
  param([hashtable[]]$Packages)
  $nl = [Environment]::NewLine
  $lines = @('## Packages','','| Package | NuGet | Install |','|---|---|---|')
  foreach ($p in $Packages) { $lines += ('| {0} | [nuget.org](https://www.nuget.org/packages/{1}) | `dotnet add package {1}` |' -f $p.Display,$p.Id) }
  return ($lines -join $nl)
}

function New-QuickStartBlock {
  $nl = [Environment]::NewLine
  $lines = @('## Quick start','', '```csharp','// using ProgesiCore;','// var v = new ProgesiVariable("Span", 35.0);','```')
  return ($lines -join $nl)
}

function New-GprBlock {
  param([string]$Owner)
  $nl = [Environment]::NewLine
  $lines = @()
  $lines += '## Using GitHub Packages (GPR)'
  $lines += ''
  $lines += '### Option A — `nuget.config`'
  $lines += ''
  $lines += 'Create a `nuget.config` next to your solution and set **GPR_PAT** env var with `read:packages` scope.'
  $lines += ''
  $lines += '```xml'
  $lines += '<configuration>'
  $lines += '  <packageSources>'
  $lines += '    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />'
  $lines += ('    <add key="github" value="https://nuget.pkg.github.com/{0}/index.json" />' -f $Owner)
  $lines += '  </packageSources>'
  $lines += '  <packageSourceCredentials>'
  $lines += '    <github>'
  $lines += ('      <add key="Username" value="{0}" />' -f $Owner)
  $lines += '      <add key="ClearTextPassword" value="%GPR_PAT%" />'
  $lines += '    </github>'
  $lines += '  </packageSourceCredentials>'
  $lines += '</configuration>'
  $lines += '```'
  $lines += ''
  $lines += 'Windows (PowerShell):'
  $lines += '```powershell'
  $lines += '$Env:GPR_PAT = "<your PAT with read:packages>"'
  $lines += ('dotnet nuget remove source github 2>$null; dotnet nuget add source "https://nuget.pkg.github.com/{0}/index.json" --name "github" --username "{0}" --password "$Env:GPR_PAT" --store-password-in-clear-text' -f $Owner)
  $lines += '```'
  $lines += ''
  $lines += 'macOS/Linux (bash):'
  $lines += '```bash'
  $lines += 'export GPR_PAT="<your PAT with read:packages>"'
  $lines += ('dotnet nuget remove source github >/dev/null 2>&1; dotnet nuget add source "https://nuget.pkg.github.com/{0}/index.json" --name "github" --username "{0}" --password "$GPR_PAT" --store-password-in-clear-text' -f $Owner)
  $lines += '```'
  return ($lines -join $nl)
}

if (Test-Path -LiteralPath $ReadmePath) { $orig = Get-Content -LiteralPath $ReadmePath -Raw } else { $orig = ('# {0}' -f $Repo) + [Environment]::NewLine }

function Set-ManagedBlock {
  param([string]$Content,[string]$BlockName,[string]$NewBlock)
  $start = '<!-- PROGESI:{0}:START -->' -f $BlockName
  $end   = '<!-- PROGESI:{0}:END -->'   -f $BlockName
  if ($Content -notmatch [regex]::Escape($start)) {
    $lines = $Content -split "`r?`n"
    $insertIdx = 0
    for ($i=0; $i -lt $lines.Count; $i++) { if ($lines[$i] -match '^\s*#\s+') { $insertIdx = $i + 1; break } }
    $managed = $start + [Environment]::NewLine + $NewBlock + [Environment]::NewLine + $end
    if ($insertIdx -gt 0 -and $insertIdx -lt $lines.Count) {
      $before = $lines[0..($insertIdx-1)]
      $after  = $lines[$insertIdx..($lines.Count-1)]
      return ($before + @('') + @($managed) + @('') + $after) -join [Environment]::NewLine
    } else { return ($managed + [Environment]::NewLine + [Environment]::NewLine + $Content) }
  } else {
    $pattern = [regex]::Escape($start) + '(?s).*?' + [regex]::Escape($end)
    $replacement = $start + [Environment]::NewLine + $NewBlock + [Environment]::NewLine + $end
    return [regex]::Replace($Content,$pattern,$replacement)
  }
}

$badgesBlock   = New-BadgeBlock -Owner $Owner -Repo $Repo -WorkflowFile $WorkflowFile -Packages $Packages
$packagesBlock = New-PackagesTableBlock -Packages $Packages
$quickStart    = New-QuickStartBlock
$gprBlock      = if ($IncludeGprSection) { New-GprBlock -Owner $Owner } else { '' }

$out = $orig
$out = Set-ManagedBlock -Content $out -BlockName 'BADGES'   -NewBlock $badgesBlock
$out = Set-ManagedBlock -Content $out -BlockName 'PACKAGES' -NewBlock $packagesBlock
if ($out -notmatch '(?im)^\s*##\s+Quick start\s*$') {
  $qsManaged = '<!-- PROGESI:QUICKSTART:START -->' + [Environment]::NewLine + $quickStart + [Environment]::NewLine + '<!-- PROGESI:QUICKSTART:END -->'
  $out = $out.TrimEnd() + [Environment]::NewLine + [Environment]::NewLine + $qsManaged
}
if ($IncludeGprSection) {
  $out = Set-ManagedBlock -Content $out -BlockName 'GPR' -NewBlock $gprBlock
}

$out = $out -replace "`r`n","`n"
$newDir = Split-Path -Parent $ReadmePath
if (-not (Test-Path $newDir)) { New-Item -ItemType Directory -Path $newDir -Force | Out-Null }
Set-Content -LiteralPath $ReadmePath -Value $out -Encoding UTF8

Write-Host 'README aggiornato: badges, packages, quick start e sezione GPR.' -ForegroundColor Green

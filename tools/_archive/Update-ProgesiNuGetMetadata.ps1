[CmdletBinding()]
param(
  [string]$Root = ".",
  [string]$LicenseExpression = "MIT",
  [string]$Tags = "bridge;engineering;progesi;grasshopper;rhino;sqlite"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Ensure-ChildNode {
  param([xml]$Xml,[System.Xml.XmlNode]$Parent,[string]$Name)
  $n = $Parent.SelectSingleNode($Name)
  if (-not $n) { $n = $Xml.CreateElement($Name); $null = $Parent.AppendChild($n) }
  return $n
}

function Set-Prop {
  param([xml]$Xml,[System.Xml.XmlNode]$PG,[string]$Name,[string]$Value)
  $node = Ensure-ChildNode -Xml $Xml -Parent $PG -Name $Name
  $node.InnerText = $Value
}

# Cerca tutti i .csproj
$csprojs = Get-ChildItem -Path $Root -Recurse -Filter *.csproj | Sort-Object FullName
if (-not $csprojs) { Write-Host "Nessun .csproj trovato sotto $Root" -ForegroundColor Yellow; exit 0 }

foreach ($proj in $csprojs) {
  Write-Host ("-> Analizzo {0}" -f $proj.FullName)
  [xml]$xml = Get-Content -LiteralPath $proj.FullName

  # PropertyGroup "principale" (crea se manca)
  $pg = $xml.Project.PropertyGroup | Select-Object -First 1
  if (-not $pg) { $pg = $xml.CreateElement("PropertyGroup"); $null = $xml.Project.AppendChild($pg) }

  # packable? (PS5 safe)
  $isPackable = $false
  $isPackableNode = $pg.SelectSingleNode("IsPackable")
  if ($isPackableNode -and $isPackableNode.InnerText) {
    $tmp = $false; $null = [bool]::TryParse($isPackableNode.InnerText, [ref]$tmp); $isPackable = $tmp
  } else {
    $pkgIdNode      = $pg.SelectSingleNode("PackageId")
    $genOnBuildNode = $pg.SelectSingleNode("GeneratePackageOnBuild")
    $genOnBuild = $false
    if ($genOnBuildNode -and $genOnBuildNode.InnerText) { $null = [bool]::TryParse($genOnBuildNode.InnerText, [ref]$genOnBuild) }
    $isPackable = ($pkgIdNode -ne $null) -or $genOnBuild
  }
  if (-not $isPackable) { Write-Host "   (skip) Non packabile" -ForegroundColor DarkGray; continue }

  # Metadata base
  $repoUrl = "https://github.com/GianlucaProgesi/Progesi"
  Set-Prop -Xml $xml -PG $pg -Name "RepositoryUrl"            -Value $repoUrl
  Set-Prop -Xml $xml -PG $pg -Name "PackageProjectUrl"        -Value $repoUrl
  Set-Prop -Xml $xml -PG $pg -Name "PackageTags"              -Value $Tags
  Set-Prop -Xml $xml -PG $pg -Name "PackageReadmeFile"        -Value "README.nuget.md"
  Set-Prop -Xml $xml -PG $pg -Name "PackageLicenseExpression" -Value $LicenseExpression
  Set-Prop -Xml $xml -PG $pg -Name "PublishRepositoryUrl"     -Value "true"
  Set-Prop -Xml $xml -PG $pg -Name "ContinuousIntegrationBuild" -Value "true"
  Set-Prop -Xml $xml -PG $pg -Name "Deterministic"            -Value "true"
  Set-Prop -Xml $xml -PG $pg -Name "IncludeSymbols"           -Value "true"
  Set-Prop -Xml $xml -PG $pg -Name "SymbolPackageFormat"      -Value "snupkg"
  Set-Prop -Xml $xml -PG $pg -Name "EmbedUntrackedSources"    -Value "true"

  # Trova un ItemGroup che già contiene PackageReference; se non esiste, creane uno.
  $ig = $null
  if ($xml.Project.ItemGroup) {
    $igs = @($xml.Project.ItemGroup)
    foreach ($g in $igs) {
      $nodes = $g.SelectNodes("PackageReference")
      if ($nodes -and $nodes.Count -gt 0) { $ig = $g; break }
    }
  }
  if (-not $ig) {
    $ig = $xml.CreateElement("ItemGroup")
    $null = $xml.Project.AppendChild($ig)
  }

  # Assicura Microsoft.SourceLink.GitHub
  $hasSL = $false
  $existingRefs = $ig.SelectNodes("PackageReference")
  if ($existingRefs) {
    foreach ($pr in $existingRefs) {
      if ($pr.GetAttribute("Include") -eq "Microsoft.SourceLink.GitHub") { $hasSL = $true; break }
    }
  }
  if (-not $hasSL) {
    $pr = $xml.CreateElement("PackageReference")
    $pr.SetAttribute("Include","Microsoft.SourceLink.GitHub")
    $pr.SetAttribute("Version","8.0.0")
    $pr.SetAttribute("PrivateAssets","All")
    $null = $ig.AppendChild($pr)
  }

  $xml.Save($proj.FullName)
  Write-Host "   OK: Metadata + SourceLink"

  # README.nuget.md (crea se manca)
  $projDir = Split-Path -Parent $proj.FullName
  $readme  = Join-Path $projDir "README.nuget.md"
  if (-not (Test-Path $readme)) {
    $pkgIdNode = $pg.SelectSingleNode("PackageId")
    $packageId = if ($pkgIdNode) { $pkgIdNode.InnerText } else { [System.IO.Path]::GetFileNameWithoutExtension($proj.Name) }

    $lines = @(
      "# $packageId",
      "",
      "NuGet package for Progesi.",
      "",
      "## Install",
      "`dotnet add package $packageId`",
      "",
      "## Quick start",
      "```csharp",
      "// var v = new ProgesiVariable(""Span"", 35.0);",
      "```",
      "",
      "## Links",
      "- Repository: https://github.com/GianlucaProgesi/Progesi",
      "- License: MIT"
    )
    Set-Content -LiteralPath $readme -Value $lines -Encoding ASCII
    Write-Host "   OK: Creato $readme"
  } else {
    Write-Host "   (skip) README.nuget.md gia' presente"
  }
}

Write-Host ""
Write-Host "Completato." -ForegroundColor Green

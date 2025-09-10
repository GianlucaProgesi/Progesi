# tools/Fix-Formatting.ps1
[CmdletBinding()]
param(
  [switch] $Push   # usa -Push per fare anche push del commit
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Ensure-DotNetFormat {
  try {
    dotnet format --version | Out-Null
  }
  catch {
    Write-Host "→ Installo/aggiorno dotnet-format come tool globale..."
    dotnet tool update -g dotnet-format | Out-Null
    $tools = Join-Path $env:USERPROFILE ".dotnet\tools"
    if ($env:PATH -notlike "*$tools*") { $env:PATH = "$env:PATH;$tools" }
  }
}

function Ensure-GitAttributes {
  $gitattributes = ".gitattributes"
  if (-not (Test-Path $gitattributes)) {
    @"
# Normalizza gli EOL e forza CRLF per i .cs su runner Windows
*       text=auto
*.cs    text eol=crlf
*.csproj text eol=crlf
*.props  text eol=crlf
*.targets text eol=crlf
*.sln   text eol=crlf
"@ | Set-Content -Encoding UTF8 $gitattributes
    git add .gitattributes | Out-Null
    Write-Host "→ Creato .gitattributes (CRLF per .cs & co.)"
  }
  Write-Host "→ Rinormalizzo gli EOL secondo .gitattributes..."
  git add --renormalize . | Out-Null
}

function Run-Format {
  Write-Host "→ Eseguo dotnet format (stile+whitespace+imports)..."
  # Se hai la soluzione .sln, usa quella; altrimenti format su cartella
  if (Test-Path ".\Progesi.sln") {
    dotnet format .\Progesi.sln --verbosity minimal
  } else {
    dotnet format --verbosity minimal
  }
}

# --- main ---
git rev-parse --is-inside-work-tree | Out-Null

Ensure-DotNetFormat
Ensure-GitAttributes
Run-Format

# Mostra diff e committa se ci sono cambi
$changes = git status --porcelain
if ($changes) {
  git commit -am "style(format): apply dotnet format + CRLF normalization" --no-verify
  Write-Host "✓ Commit di formattazione creato."
  if ($Push) {
    $branch = (git rev-parse --abbrev-ref HEAD).Trim()
    Write-Host "→ Push su origin/$branch..."
    git push -u origin $branch
  } else {
    Write-Host "ℹ Usa -Push per fare anche il push:  pwsh -File tools/Fix-Formatting.ps1 -Push"
  }
} else {
  Write-Host "✓ Nessuna modifica da committare (già formattato)."
}

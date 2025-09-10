<#
.SYNOPSIS
  Abilita la raccolta di coverage XPlat nel progetto di test ProgesiCore.Tests.

.DESCRIPTION
  - Aggiunge i pacchetti necessari (coverlet.collector, Microsoft.NET.Test.Sdk).
  - Fa commit & push usando Go-CI.ps1.
  - Esegue un run locale di dotnet test con XPlat Code Coverage per validazione.
#>

$testProj = "tests/ProgesiCore.Tests/ProgesiCore.Tests.csproj"

try {
  Write-Host "== Aggiungo pacchetti necessari ==" -ForegroundColor Yellow
  dotnet add $testProj package coverlet.collector --version 6.0.0
  dotnet add $testProj package Microsoft.NET.Test.Sdk --version 17.11.1

  Write-Host "== Commit & Push ==" -ForegroundColor Yellow
  pwsh -File tools/Go-CI.ps1 -Message "test: enable XPlat Code Coverage in ProgesiCore.Tests" -OpenRun

  Write-Host "== Eseguo un run locale per verifica ==" -ForegroundColor Yellow
  dotnet test $testProj -c Release --no-build --collect:"XPlat Code Coverage" -l "trx;LogFileName=Local.trx"

  Write-Host "Coverage abilitato con successo." -ForegroundColor Green
}
catch {
  Write-Error $_.Exception.Message
  exit 1
}

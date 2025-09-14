param(
  [string]$Cobertura = "TestResults/MergedCoverage/Cobertura.xml",
  [int]$Top = 20
)

if (!(Test-Path $Cobertura)) {
  Write-Error "Cobertura non trovato: $Cobertura"
  exit 1
}

[xml]$doc = Get-Content $Cobertura

# coverage -> packages -> package -> classes -> class
$classes = @($doc.coverage.packages.package.classes.class)

$rows = foreach ($cls in $classes) {
  $lines   = @($cls.lines.line)
  $valid   = [int]$lines.Count
  $covered = [int](@($lines | Where-Object { [int]$_.hits -gt 0 }).Count)
  $pct     = if ($valid -gt 0) { [math]::Round(($covered/$valid)*100, 2) } else { 0 }

  $methods = @($cls.methods.method)
  $notCov  = $methods | Where-Object {
    $m = @($_.lines.line); $m.Count -gt 0 -and (@($m | Where-Object { [int]$_.hits -gt 0 }).Count) -eq 0
  } | Select-Object -ExpandProperty name -ErrorAction SilentlyContinue

  [pscustomobject]@{
    File              = "$($cls.filename)"
    Class             = "$($cls.name)"
    LinePct           = $pct
    LinesNotCovered   = $valid - $covered
    LinesValid        = $valid
    MethodsNotCovered = ($notCov -join ", ")
  }
}

# Ordino per: LinePct (crescente) poi LinesValid (decrescente) – due passaggi (compatibile PS 5.1)
$worst = $rows | Sort-Object LinesValid -Descending | Sort-Object LinePct | Select-Object -First $Top

$worst | Format-Table -AutoSize

# Step summary per GitHub
if ($env:GITHUB_STEP_SUMMARY) {
  $sb = New-Object System.Text.StringBuilder
  $null = $sb.AppendLine("### Worst files by line coverage")
  $null = $sb.AppendLine("")
  $null = $sb.AppendLine("| File | Class | Line % | Uncovered | Lines | Not covered methods |")
  $null = $sb.AppendLine("|---|---|---:|---:|---:|---|")
  foreach($r in $worst) {
    $null = $sb.AppendLine("| $($r.File) | $($r.Class) | $($r.LinePct)% | $($r.LinesNotCovered) | $($r.LinesValid) | $($r.MethodsNotCovered) |")
  }
  Set-Content -Path $env:GITHUB_STEP_SUMMARY -Value $sb.ToString() -Encoding UTF8
}

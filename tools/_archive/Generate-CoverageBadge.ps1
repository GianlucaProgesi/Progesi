param(
  [Parameter(Mandatory=$true)][string]$SummaryPath,
  [Parameter(Mandatory=$true)][string]$OutPath
)

if (-not (Test-Path $SummaryPath)) {
  throw "Summary non trovato: $SummaryPath"
}

$text = Get-Content -Raw $SummaryPath
$line = if ($text -match 'Line coverage:\s*([\d\.]+)%') { [double]$Matches[1] } else { 0 }
$branch = if ($text -match 'Branch coverage:\s*([\d\.]+)%') { [double]$Matches[1] } else { 0 }
$method = if ($text -match 'Method coverage:\s*([\d\.]+)%') { [double]$Matches[1] } else { 0 }

# Mostriamo il Line coverage come valore principale (come da baseline)
$val = "{0:N1}%" -f $line

# Colore semplice a soglie (verde/giallo/rosso)
$color = if ($line -ge 90) { "#4c1" } elseif ($line -ge 80) { "#dfb317" } else { "#e05d44" }

$svg = @"
<svg xmlns="http://www.w3.org/2000/svg" width="180" height="20" role="img" aria-label="coverage: $val">
  <linearGradient id="s" x2="0" y2="100%">
    <stop offset="0" stop-color="#bbb" stop-opacity=".1"/>
    <stop offset="1" stop-opacity=".1"/>
  </linearGradient>
  <mask id="m">
    <rect width="180" height="20" rx="3" fill="#fff"/>
  </mask>
  <g mask="url(#m)">
    <rect width="86" height="20" fill="#555"/>
    <rect x="86" width="94" height="20" fill="$color"/>
    <rect width="180" height="20" fill="url(#s)"/>
  </g>
  <g fill="#fff" text-anchor="middle"
     font-family="Verdana,Geneva,DejaVu Sans,sans-serif" font-size="11">
    <text x="43" y="15">coverage</text>
    <text x="132" y="15">$val</text>
  </g>
</svg>
"@

# Ensure folder
$dir = Split-Path -Parent $OutPath
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }

$svg | Set-Content -Path $OutPath -Encoding UTF8 -NoNewline
Write-Host "Badge scritto in $OutPath (line=$line, branch=$branch, method=$method)"

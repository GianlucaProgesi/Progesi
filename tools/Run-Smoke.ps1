Param(
  [string]$OutDir = "out\smoke"
)

$ErrorActionPreference = "Stop"

# --- prerequisito: sqlite3 nel PATH (winget lo ha installato) ---
$sqlite3 = $null
try { $sqlite3 = (Get-Command sqlite3 -ErrorAction Stop).Source } catch { }
if (-not $sqlite3) { throw "sqlite3.exe non trovato nel PATH. Installa con: winget install -e --id SQLite.SQLite" }

# --- cartelle/percorsi ---
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$db = Join-Path $OutDir "progesi_smoke.db"
$report = Join-Path $OutDir "smoke-report.txt"

if (Test-Path $db) { Remove-Item $db -Force }
if (Test-Path $report) { Remove-Item $report -Force }

# --- schema + seed  (ATTENZIONE: "Values" è riservata) ---
$schema = @"
CREATE TABLE IF NOT EXISTS variables (
  Id TEXT PRIMARY KEY, Hash TEXT, Name TEXT, Value TEXT, Unit TEXT, By TEXT, Ref TEXT, LastModifiedUtc TEXT
);
CREATE TABLE IF NOT EXISTS metadata (
  Id TEXT PRIMARY KEY, Hash TEXT, Info TEXT, By TEXT, Ref TEXT, LastModifiedUtc TEXT
);
CREATE TABLE IF NOT EXISTS axisvariables (
  Id TEXT PRIMARY KEY, Hash TEXT, Name TEXT, Unit TEXT, AxisRef TEXT, Stations TEXT, "Values" TEXT, By TEXT, Ref TEXT, LastModifiedUtc TEXT
);
CREATE INDEX IF NOT EXISTS idx_variables_hash ON variables(Hash);
CREATE INDEX IF NOT EXISTS idx_metadata_hash ON metadata(Hash);
CREATE INDEX IF NOT EXISTS idx_axis_hash     ON axisvariables(Hash);
"@

$seed = @"
INSERT INTO variables (Id,Hash,Name,Value,Unit,By,Ref,LastModifiedUtc)
VALUES ('v-1','','E1_Load','42.5','kN','Smoke','','2020-01-01T00:00:00Z');

INSERT INTO metadata (Id,Hash,Info,By,Ref,LastModifiedUtc)
VALUES ('m-1','','Seed metadata: project alpha','Smoke','','2020-01-01T00:00:00Z');

INSERT INTO axisvariables (Id,Hash,Name,Unit,AxisRef,Stations,""Values"",By,Ref,LastModifiedUtc)
VALUES ('a-1','','GirderCamber','mm','Axis-1','0;0.5;1','0;15;0','Smoke','','2020-01-01T00:00:00Z');
"@

# --- crea DB + esegui schema/seed ---
& $sqlite3 $db $schema | Out-Null
& $sqlite3 $db $seed   | Out-Null

# --- assert P0 ---
$ok = $true
$lines = New-Object System.Collections.Generic.List[string]

function Add-Line([string]$s) { $lines.Add($s) }

# 1) variables: riga E1_Load con unit kN e value '42.5'
$v = & $sqlite3 $db "SELECT COUNT(*) FROM variables WHERE Name='E1_Load' AND Unit='kN' AND Value='42.5';"
if ($v -eq "1") { Add-Line "OK variables: E1_Load=42.5 kN" } else { Add-Line "FAIL variables: atteso 1, trovato $v"; $ok = $false }

# 2) metadata: una riga con 'Seed metadata'
$m = & $sqlite3 $db "SELECT COUNT(*) FROM metadata WHERE Info LIKE '%Seed metadata%';"
if ($m -ge "1") { Add-Line "OK metadata: seed presente ($m)" } else { Add-Line "FAIL metadata: seed mancante"; $ok = $false }

# 3) axis: GirderCamber con Stations '0;0.5;1' e Values '0;15;0'
$a = & $sqlite3 $db "SELECT COUNT(*) FROM axisvariables WHERE Name='GirderCamber' AND Stations='0;0.5;1' AND ""Values""='0;15;0';"
if ($a -eq "1") { Add-Line "OK axis: GirderCamber con stations/values corretti" } else { Add-Line "FAIL axis: atteso 1, trovato $a"; $ok = $false }

# --- scrivi report e esci ---
$summary = "Smoke P0 - $(Get-Date -Format s)Z  Result: " + ($(if ($ok) {"PASS"} else {"FAIL"}))
Add-Line $summary
$lines | Set-Content -Path $report -Encoding UTF8

if ($ok) { Write-Host "✅ SMOKE PASS – report: $report"; exit 0 } else { Write-Host "❌ SMOKE FAIL – report: $report"; exit 1 }

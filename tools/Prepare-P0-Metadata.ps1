$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$tests = Join-Path $root "..\tests\P0-Metadata\mock"
New-Item -ItemType Directory -Force -Path $tests | Out-Null

# due mock "deterministici"
$mock1 = @{
  id = 1; hash = "mock-00000001"; by = "GM"; info = "Mock metadata #1";
  refs = @("https://example.org/metadata/1", "file:///C:/Progesi/mock/1.png");
  snips = @("snip:1:image/png:caption=Mock-1");
  lastModified = "2025-09-29T01:00:00Z"
} | ConvertTo-Json -Depth 4
$mock2 = @{
  id = 2; hash = "mock-00000002"; by = "GM"; info = "Mock metadata #2";
  refs = @("https://example.org/metadata/2", "file:///C:/Progesi/mock/2.png");
  snips = @("snip:2:image/png:caption=Mock-2");
  lastModified = "2025-09-29T01:00:00Z"
} | ConvertTo-Json -Depth 4

Set-Content -Path (Join-Path $tests "mock-00000001.json") -Value $mock1 -Encoding UTF8
Set-Content -Path (Join-Path $tests "mock-00000002.json") -Value $mock2 -Encoding UTF8

@"
# P0 – Metadata (Mock)

## MetIn
- Create: Run=True, Act='Create', By='GM', Info='This is a test', Ref=<url/path>, Snip=<base64/url/path>
  - atteso: Id=1, Hash=<non vuoto>, Info='OK'
- Update: Run=True, Act='Update', Id=1, ... 
  - atteso: Id=1, Info='OK'
- Delete: Run=True, Act='Delete', Id=1
  - atteso: Id=1, Info='OK'

## MetOut
- Get by Hash: Run=True, Hash='mock-00000001'
  - atteso: Id=1, Info='OK', Refs e Snips valorizzati, LastModified='2025-09-29T01:00:00Z'
- Get by Id:   Run=True, Id=2 (Hash vuoto)
  - atteso: Id=2, Info='OK', Refs e Snips valorizzati
"@ | Set-Content -Path (Join-Path $tests "P0-checklist.md") -Encoding UTF8

Write-Host "Mock P0 pronti in: $tests"

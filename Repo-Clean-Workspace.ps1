Param([string]$Root = ".")
$ErrorActionPreference = "Stop"
Push-Location $Root
$dirs = @("bin","obj",".vs",".idea",".vscode","out","TestResults","artifacts",".cache","_UpgradeBackup")
Get-ChildItem -Directory -Recurse -ErrorAction SilentlyContinue | Where-Object { $dirs -contains $_.Name -or $_.Name -like 'coverage*' } | ForEach-Object {
  try { Remove-Item $_.FullName -Recurse -Force -ErrorAction Stop; Write-Host "Removed: $($_.FullName)" } catch { Write-Warning $_.Exception.Message }
}
Get-ChildItem -Recurse -Include *.user,*.suo,*.tmp,*.log,*.db-wal,*.db-shm,Thumbs.db,.DS_Store -ErrorAction SilentlyContinue |
  ForEach-Object { try { Remove-Item $_.FullName -Force -ErrorAction Stop } catch { Write-Warning $_.Exception.Message } }
Pop-Location
Write-Host "✅ Pulizia completata"

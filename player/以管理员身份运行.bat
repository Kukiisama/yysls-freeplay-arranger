@echo off
setlocal
cd /d "%~dp0"

powershell -NoProfile -ExecutionPolicy Bypass -Command "try { $exe = Get-ChildItem -LiteralPath '%~dp0' -Filter '*.exe' | Sort-Object LastWriteTime -Descending | Select-Object -First 1; if (-not $exe) { throw 'player exe not found' }; Start-Process -FilePath $exe.FullName -Verb RunAs -ErrorAction Stop } catch { Write-Host $_.Exception.Message; exit 1 }"

if errorlevel 1 (
  echo.
  echo [YYSLS] Failed to start as administrator.
  echo UAC may have been cancelled, or PowerShell is blocked.
  pause
)
endlocal

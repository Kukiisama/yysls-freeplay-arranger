@echo off
setlocal
for %%F in ("%~dp0arranger\*.html") do (
  start "" "%%~fF"
  exit /b 0
)
echo [YYSLS] Cannot find arranger html.
pause
endlocal

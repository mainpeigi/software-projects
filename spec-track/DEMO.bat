@echo off
setlocal
cd /d "%~dp0"
if not exist "SpecTrack.Api.exe" (
  echo This folder contains source code. Use the demo command from README.md.
  pause
  exit /b 1
)
SpecTrack.Api.exe --demo
if errorlevel 1 (
  echo Demo failed. See the message above.
) else (
  echo Open storage\demo-report.html in a browser to see the change report.
)
pause

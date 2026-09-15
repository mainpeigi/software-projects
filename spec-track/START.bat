@echo off
setlocal
cd /d "%~dp0"
if not exist "SpecTrack.Api.exe" (
  echo This folder contains source code. Use START-SOURCE.bat or the Windows portable package.
  pause
  exit /b 1
)
echo SpecTrack API: http://127.0.0.1:5192
echo Keep this window open. Press Ctrl+C to stop.
SpecTrack.Api.exe
pause

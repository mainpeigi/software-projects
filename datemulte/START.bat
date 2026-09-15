@echo off
setlocal
cd /d "%~dp0"
if not exist "Datemulte_2.exe" (
  echo This is the source package. Use START-SOURCE.bat or the Windows portable ZIP.
  pause
  exit /b 1
)
echo Datemulte - local edition
echo Open http://127.0.0.1:5188 in your browser.
echo Keep this window open while using the application. Press Ctrl+C to stop.
Datemulte_2.exe
pause

@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
  echo Install the .NET 9 SDK to run from source, or use the ready Windows ZIP.
  pause
  exit /b 1
)
echo Open http://127.0.0.1:5188 in your browser once the server starts.
dotnet run --project Datemulte_2.csproj --configuration Release --no-launch-profile
pause

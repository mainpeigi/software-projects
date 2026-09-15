@echo off
setlocal
cd /d "%~dp0"
echo SpecTrack API: http://127.0.0.1:5192
dotnet run --project src/SpecTrack.Api/SpecTrack.Api.csproj --configuration Release --no-launch-profile
pause

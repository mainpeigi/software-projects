@echo off
setlocal
cd /d "%~dp0"
dotnet publish src/SpecTrack.Api/SpecTrack.Api.csproj --configuration Release --runtime win-x64 --self-contained true --output publish/windows-x64
if errorlevel 1 exit /b 1
copy /y START.bat publish\windows-x64\START.bat >nul
copy /y DEMO.bat publish\windows-x64\DEMO.bat >nul
copy /y TRANSFER.md publish\windows-x64\TRANSFER.md >nul
echo Ready: publish\windows-x64. Copy the complete folder to another Windows x64 laptop.

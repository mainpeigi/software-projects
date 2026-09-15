$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
dotnet publish Datemulte_2.csproj -c Release -r win-x64 --self-contained true -o publish/windows-x64
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
Copy-Item -LiteralPath START.bat, TRANSFER.md -Destination publish/windows-x64
Copy-Item -LiteralPath samples -Destination publish/windows-x64 -Recurse -Force
Write-Host 'Ready: publish/windows-x64. Copy the whole folder to another Windows x64 laptop.'

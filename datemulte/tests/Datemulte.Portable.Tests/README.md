# Portable integration tests

Run from the `datemulte` folder with a .NET 9 SDK or newer:

```powershell
dotnet test tests/Datemulte.Portable.Tests/Datemulte.Portable.Tests.csproj --configuration Release
```

The tests start the real application in ASP.NET Core's in-process test server. Each test uses a fresh temporary data folder and disables PostgreSQL and Supabase connection settings. The data folder is configured before the application reads its startup settings. Temporary files and SQLite connections are cleaned up after the application is disposed.

Coverage includes the health endpoint and SQLite file location; home, plotter and profile pages; local ECharts, Blazor and MudBlazor assets; CSV upload and numerical statistics; selecting a worksheet in a real Excel file; local profile persistence; and session edits after application restart and copying the complete data folder. The copy test also checks that deleting the transferred session removes the transferred source file while preserving the original copy.

These are HTTP and service integration tests. Browser interaction and rendering are checked separately during release verification.

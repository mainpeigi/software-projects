# Datemulte — portable local edition

A C# / Blazor application for importing CSV and Excel data, drawing interactive charts, calculating statistics, editing data and saving analysis sessions.

This edition runs on one laptop. Sessions and uploaded files are stored in `local-data/`, using an embedded SQLite database. It needs no PostgreSQL server, Supabase account, database password or Internet connection at runtime. ECharts and its themes are included locally.

## Run the ready Windows package

Extract **all** of `datemulte-windows-portable.zip` into a writable folder. Double-click `START.bat`, then open **http://127.0.0.1:5188**. Keep the console open while using the app. The Windows x64 package includes .NET; no SDK, database server or Docker installation is needed.

## Run from this repository

Install the .NET 9 SDK (a compatible newer SDK also works), then:

```powershell
cd datemulte
dotnet run --project Datemulte_2.csproj --configuration Release --no-launch-profile
```

The first build downloads NuGet packages. Open http://127.0.0.1:5188. On Windows, `START-SOURCE.bat` runs the same command.

## Demonstration

1. Open Data Plotter and upload `samples/temperatures.csv`.
2. Click **Load File**, then select `Temperature` as a plotted column.
3. Explore the chart and statistical tools; optionally select `Humidity` on the second axis.
4. Enter a session name and click **Save Session**.
5. Stop and restart the application, then use **Load Session** to restore the data and saved chart settings.

The sample is synthetic. It contains eight rows; the mean temperature is **23.5**, minimum **20**, maximum **27**.

## Transfer your work

Stop the application before copying its complete folder, including `local-data/`. Uploaded files use relative names, so sessions remain valid when the folder moves. Source users should copy their source folder and its `local-data/` directory. Browser-only preferences and unsaved work do not transfer; save each session first.

See [TRANSFER.md](TRANSFER.md) for Romanian instructions.

## Verify and build a Windows package

```powershell
dotnet test tests/Datemulte.Portable.Tests/Datemulte.Portable.Tests.csproj --configuration Release
powershell -ExecutionPolicy Bypass -File .\PUBLISH-WINDOWS.ps1
```

The publish script creates `publish/windows-x64/`, a self-contained Windows x64 folder. `local-data/` is created on first start and is excluded from Git.

## Scope and provenance

The original project contains cloud-account, sharing, API, webhook and scheduled-report code. Those services are **not exposed by this local host**; online collaboration and cloud account operations are unavailable. The local profile identifies files on this machine and is not an authenticated cloud user. The server binds only to loopback.

Based on the Datemulte source snapshot `ffae00b40ae042a0f92df4f2c8382fc980355eb0`, with history attributed to Husaru Serban-Adrian / Husaru. The portable adaptation replaces deployment configuration, startup/storage and account-facing UI while retaining the original analysis and plotting code. See [docs/PROVENANCE.md](docs/PROVENANCE.md). Describe your own contribution accurately when presenting the project.

Third-party packages retain their respective licenses. Bundled Apache ECharts 5.4.3 includes its `LICENSE` and `NOTICE` in `wwwroot/lib/echarts/`.

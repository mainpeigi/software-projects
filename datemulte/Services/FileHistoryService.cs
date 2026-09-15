using Datemulte_2.Models;
using Microsoft.JSInterop;
using System.Text.Json;
using System.Security.Cryptography;

namespace Datemulte_2.Services;

public class FileHistoryService
{
    private readonly IJSRuntime _jsRuntime;
    private const string RECENT_FILES_KEY = "csvplotter_recent_files";
    private const string FILE_STATE_PREFIX = "csvplotter_file_";
    private const int MAX_RECENT_FILES = 10;

    public FileHistoryService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    // Get list of recent files
    public async Task<List<RecentFileInfo>> GetRecentFilesAsync()
    {
        try
        {
            var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", RECENT_FILES_KEY);
            if (string.IsNullOrEmpty(json))
            {
                return new List<RecentFileInfo>();
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var recentFiles = JsonSerializer.Deserialize<List<RecentFileInfo>>(json, options);
            return recentFiles ?? new List<RecentFileInfo>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading recent files: {ex.Message}");
            return new List<RecentFileInfo>();
        }
    }

    // Add a file to recent files list
    public async Task AddRecentFileAsync(string fileName, long fileSize)
    {
        try
        {
            var recentFiles = await GetRecentFilesAsync();
            var fileId = GenerateFileId(fileName, fileSize);

            // Remove if already exists
            recentFiles.RemoveAll(f => f.FileId == fileId);

            // Add to beginning
            recentFiles.Insert(0, new RecentFileInfo
            {
                FileId = fileId,
                FileName = fileName,
                FileSize = fileSize,
                LastAccessed = DateTime.UtcNow
            });

            // Keep only MAX_RECENT_FILES
            if (recentFiles.Count > MAX_RECENT_FILES)
            {
                recentFiles = recentFiles.Take(MAX_RECENT_FILES).ToList();
            }

            var json = JsonSerializer.Serialize(recentFiles);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", RECENT_FILES_KEY, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding recent file: {ex.Message}");
        }
    }

    // Save state for a specific file
    public async Task SaveFileStateAsync(string fileName, long fileSize, CsvPlotterState state)
    {
        try
        {
            var fileId = GenerateFileId(fileName, fileSize);
            var key = FILE_STATE_PREFIX + fileId;

            var fileState = new FileState
            {
                FileId = fileId,
                FileName = fileName,
                FileSize = fileSize,
                LastSaved = DateTime.UtcNow,
                SelectedColumnsY1 = state.SelectedColumnsY1.ToList(),
                SelectedColumnsY2 = state.SelectedColumnsY2.ToList(),
                SamplingLevel = state.SamplingLevel,
                EnableY2Axis = state.EnableY2Axis,
                ChartLayout = state.ChartLayout,
                SplitLevel = state.SplitLevel,
                ChartAssignments = state.ChartAssignments,
                RangeStartPoint = state.RangeStartPoint,
                RangeEndPoint = state.RangeEndPoint,
                CalculatedColumns = state.CalculatedColumns,
                CalculationFormulas = state.CalculationFormulas,
                TimeColumn = state.TimeColumn,
                NumericColumns = state.NumericColumns,
                ColumnUnits = state.ColumnUnits,
                SensorGroups = state.SensorGroups,
                SelectedSensorGroup = state.SelectedSensorGroup,
                ColumnToGroupMapping = state.ColumnToGroupMapping
            };

            var json = JsonSerializer.Serialize(fileState);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, json);

            Console.WriteLine($"[FileHistory] Saved state for file: {fileName} (ID: {fileId})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving file state: {ex.Message}");
        }
    }

    // Load state for a specific file
    public async Task<FileState?> LoadFileStateAsync(string fileName, long fileSize)
    {
        try
        {
            var fileId = GenerateFileId(fileName, fileSize);
            var key = FILE_STATE_PREFIX + fileId;

            var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", key);
            if (string.IsNullOrEmpty(json))
            {
                Console.WriteLine($"[FileHistory] No saved state found for file: {fileName}");
                return null;
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var fileState = JsonSerializer.Deserialize<FileState>(json, options);
            Console.WriteLine($"[FileHistory] Loaded state for file: {fileName} (ID: {fileId})");
            return fileState;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading file state: {ex.Message}");
            return null;
        }
    }

    // Clear state for a specific file
    public async Task ClearFileStateAsync(string fileName, long fileSize)
    {
        try
        {
            var fileId = GenerateFileId(fileName, fileSize);
            var key = FILE_STATE_PREFIX + fileId;
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
            Console.WriteLine($"[FileHistory] Cleared state for file: {fileName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clearing file state: {ex.Message}");
        }
    }

    // Remove file from recent files list
    public async Task RemoveRecentFileAsync(string fileId)
    {
        try
        {
            var recentFiles = await GetRecentFilesAsync();
            recentFiles.RemoveAll(f => f.FileId == fileId);

            var json = JsonSerializer.Serialize(recentFiles);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", RECENT_FILES_KEY, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error removing recent file: {ex.Message}");
        }
    }

    // Clear all recent files
    public async Task ClearRecentFilesAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", RECENT_FILES_KEY);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clearing recent files: {ex.Message}");
        }
    }

    // Generate a unique file ID based on name and size using SHA256 (more secure than MD5)
    private string GenerateFileId(string fileName, long fileSize)
    {
        var input = $"{fileName}_{fileSize}";
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(inputBytes);
        return Convert.ToHexString(hash).ToLower()[..16];
    }
}

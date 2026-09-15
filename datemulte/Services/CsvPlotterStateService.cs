using Datemulte_2.Models;
using Microsoft.JSInterop;
using System.Text.Json;

namespace Datemulte_2.Services;

public class CsvPlotterStateService
{
    private readonly IJSRuntime _jsRuntime;
    private const string SELECTIONS_KEY_PREFIX = "csvplotter_selections";
    
    private CsvPlotterState? _currentState;
    private Guid? _currentSessionId;
    private string? _currentFileName;
    
    public CsvPlotterStateService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }
    
    /// <summary>
    /// Sets the current session ID for session-aware state storage.
    /// Call this when loading or creating a database session.
    /// This takes precedence over file-based tracking.
    /// </summary>
    public void SetCurrentSession(Guid? sessionId)
    {
        // If switching sessions, clear in-memory state to prevent cross-contamination
        if (_currentSessionId != sessionId)
        {
            _currentState = null;
        }
        _currentSessionId = sessionId;
    }
    
    /// <summary>
    /// Sets the current file name for file-based state storage.
    /// Call this when loading a file directly (without creating a database session).
    /// </summary>
    public void SetCurrentFile(string? fileName)
    {
        // If switching files, clear in-memory state to prevent cross-contamination
        if (_currentFileName != fileName)
        {
            _currentState = null;
        }
        _currentFileName = fileName;
        
        // Clear session when switching to file-based mode
        if (!string.IsNullOrEmpty(fileName))
        {
            _currentSessionId = null;
        }
    }
    
    /// <summary>
    /// Sets the current file and loads any previously saved state for that file.
    /// Returns the saved state if found, or null if no previous state exists.
    /// Use this when switching to a file to restore its settings.
    /// </summary>
    public async Task<CsvPlotterState?> SetCurrentFileAndLoadStateAsync(string fileName)
    {
        SetCurrentFile(fileName);
        
        // Force load from localStorage (bypass in-memory cache since we just cleared it)
        var savedState = await LoadSelectionsFromLocalStorageAsync();
        if (savedState != null)
        {
            _currentState = savedState;
        }
        
        return savedState;
    }
    
    /// <summary>
    /// Gets the current session ID.
    /// </summary>
    public Guid? GetCurrentSessionId() => _currentSessionId;
    
    /// <summary>
    /// Gets the current file name.
    /// </summary>
    public string? GetCurrentFileName() => _currentFileName;
    
    /// <summary>
    /// Gets the localStorage key based on current session or file.
    /// Priority: Session ID > File Name > Default key
    /// </summary>
    private string GetStorageKey()
    {
        if (_currentSessionId.HasValue)
        {
            return $"{SELECTIONS_KEY_PREFIX}_session_{_currentSessionId.Value}";
        }
        
        if (!string.IsNullOrEmpty(_currentFileName))
        {
            // Create a safe key from the file name (remove invalid characters)
            var safeFileName = GetSafeKeyFromFileName(_currentFileName);
            return $"{SELECTIONS_KEY_PREFIX}_file_{safeFileName}";
        }
        
        return SELECTIONS_KEY_PREFIX;
    }
    
    /// <summary>
    /// Creates a safe localStorage key from a file name.
    /// </summary>
    private static string GetSafeKeyFromFileName(string fileName)
    {
        // Remove path if present, keep just the file name
        var name = Path.GetFileName(fileName);
        
        // Replace any characters that might cause issues in localStorage keys
        var invalidChars = new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|', ' ' };
        foreach (var c in invalidChars)
        {
            name = name.Replace(c, '_');
        }
        
        return name.ToLowerInvariant();
    }
    
    public bool HasData()
    {
        return _currentState?.Data?.Count > 0;
    }
    
    public CsvPlotterState GetState()
    {
        return _currentState ?? new CsvPlotterState();
    }
    
    public void SaveState(CsvPlotterState state)
    {
        _currentState = state;
        _ = SaveSelectionsToLocalStorageAsync(state);
    }
    
    public async Task<CsvPlotterState> LoadStateAsync()
    {
        if (_currentState != null && _currentState.Data.Count > 0)
        {
            return _currentState;
        }
        
        var selections = await LoadSelectionsFromLocalStorageAsync();
        if (selections != null)
        {
            _currentState = selections;
            return _currentState;
        }
        
        return new CsvPlotterState();
    }
    
    public void ClearState()
    {
        _currentState = null;
        _ = ClearLocalStorageAsync();
    }
    
    /// <summary>
    /// Clears state for a specific session (useful when deleting sessions).
    /// </summary>
    public async Task ClearSessionStateAsync(Guid sessionId)
    {
        try
        {
            var key = $"{SELECTIONS_KEY_PREFIX}_session_{sessionId}";
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clearing session state: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Clears state for a specific file (useful when you want to reset a file's settings).
    /// </summary>
    public async Task ClearFileStateAsync(string fileName)
    {
        try
        {
            var safeFileName = GetSafeKeyFromFileName(fileName);
            var key = $"{SELECTIONS_KEY_PREFIX}_file_{safeFileName}";
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clearing file state: {ex.Message}");
        }
    }
    
    private async Task SaveSelectionsToLocalStorageAsync(CsvPlotterState state)
    {
        try
        {
            var selectionsData = new
            {
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
                CalculationCache = state.CalculationCache,
                FileName = state.FileName,
                TimeColumn = state.TimeColumn,
                NumericColumns = state.NumericColumns,
                LastFileLoadTimestamp = state.LastFileLoadTimestamp,
                LastFileSize = state.LastFileSize,
                ColumnUnits = state.ColumnUnits
            };
            
            var json = JsonSerializer.Serialize(selectionsData);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", GetStorageKey(), json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving selections: {ex.Message}");
        }
    }
    
    private async Task<CsvPlotterState?> LoadSelectionsFromLocalStorageAsync()
    {
        try
        {
            var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", GetStorageKey());
            
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }
            
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, options);
            
            if (data == null) return null;
            
            var state = new CsvPlotterState();
            
            if (data.ContainsKey("SelectedColumnsY1"))
            {
                state.SelectedColumnsY1 = JsonSerializer.Deserialize<HashSet<string>>(data["SelectedColumnsY1"].GetRawText()) ?? new HashSet<string>();
            }
            
            if (data.ContainsKey("SelectedColumnsY2"))
            {
                state.SelectedColumnsY2 = JsonSerializer.Deserialize<HashSet<string>>(data["SelectedColumnsY2"].GetRawText()) ?? new HashSet<string>();
            }
            
            if (data.ContainsKey("SamplingLevel"))
            {
                state.SamplingLevel = data["SamplingLevel"].GetInt32();
            }
            
            if (data.ContainsKey("EnableY2Axis"))
            {
                state.EnableY2Axis = data["EnableY2Axis"].GetBoolean();
            }
            
            if (data.ContainsKey("ChartLayout"))
            {
                state.ChartLayout = data["ChartLayout"].GetString() ?? "single";
            }
            
            if (data.ContainsKey("SplitLevel"))
            {
                state.SplitLevel = data["SplitLevel"].GetInt32();
            }
            
            if (data.ContainsKey("ChartAssignments"))
            {
                state.ChartAssignments = JsonSerializer.Deserialize<Dictionary<string, int>>(data["ChartAssignments"].GetRawText()) ?? new Dictionary<string, int>();
            }
            
            if (data.ContainsKey("RangeStartPoint"))
            {
                state.RangeStartPoint = data["RangeStartPoint"].GetInt32();
            }
            
            if (data.ContainsKey("RangeEndPoint"))
            {
                state.RangeEndPoint = data["RangeEndPoint"].GetInt32();
            }
            
            if (data.ContainsKey("CalculatedColumns"))
            {
                state.CalculatedColumns = JsonSerializer.Deserialize<List<string>>(data["CalculatedColumns"].GetRawText()) ?? new List<string>();
            }
            
            if (data.ContainsKey("CalculationFormulas"))
            {
                state.CalculationFormulas = JsonSerializer.Deserialize<Dictionary<string, CalculationFormula>>(data["CalculationFormulas"].GetRawText()) ?? new Dictionary<string, CalculationFormula>();
            }
            
            if (data.ContainsKey("FileName"))
            {
                state.FileName = data["FileName"].GetString();
            }
            
            if (data.ContainsKey("TimeColumn"))
            {
                state.TimeColumn = data["TimeColumn"].GetString();
            }
            
            if (data.ContainsKey("NumericColumns"))
            {
                state.NumericColumns = JsonSerializer.Deserialize<List<string>>(data["NumericColumns"].GetRawText()) ?? new List<string>();
            }
            
            if (data.ContainsKey("CalculationCache"))
            {
                state.CalculationCache = JsonSerializer.Deserialize<Dictionary<string, List<double>>>(data["CalculationCache"].GetRawText()) ?? new Dictionary<string, List<double>>();
            }
            
            if (data.ContainsKey("LastFileLoadTimestamp"))
            {
                var tsString = data["LastFileLoadTimestamp"].GetString();
                if (!string.IsNullOrEmpty(tsString) && DateTime.TryParse(tsString, out var ts))
                {
                    state.LastFileLoadTimestamp = ts;
                }
            }
            
            if (data.ContainsKey("LastFileSize"))
            {
                state.LastFileSize = data["LastFileSize"].GetInt64();
            }
            
            if (data.ContainsKey("ColumnUnits"))
            {
                state.ColumnUnits = JsonSerializer.Deserialize<Dictionary<string, string>>(data["ColumnUnits"].GetRawText()) ?? new Dictionary<string, string>();
            }
            
            return state;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading selections: {ex.Message}");
            return null;
        }
    }
    
    private async Task ClearLocalStorageAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", GetStorageKey());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clearing selections: {ex.Message}");
        }
    }
}


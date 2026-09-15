using Datemulte_2.Models;
using Microsoft.JSInterop;
using System.Text.Json;

namespace Datemulte_2.Services;

public class SessionStorageService
{
    private readonly IJSRuntime _jsRuntime;
    private bool _initialized = false;

    public SessionStorageService(IJSRuntime jSRuntime)
    {
        _jsRuntime = jSRuntime;
    }
    
    public async Task EnsureInitializedAsync()
    {
        if (!_initialized)
        {
            await _jsRuntime.InvokeVoidAsync("indexedDbStorage.init");
            _initialized = true;
        }
    }

    public async Task<string> SaveSessionAsync(SessionState session)
    {
        await EnsureInitializedAsync();
        var json = JsonSerializer.Serialize(session);
        return await _jsRuntime.InvokeAsync<string>("indexedDbStorage.saveSession", json);
    }

    public async Task<SessionState?> LoadSessionAsync(string id)
    {
        await EnsureInitializedAsync();
        var json = await _jsRuntime.InvokeAsync<string>("indexedDbStorage.loadSession", id);
        if (string.IsNullOrEmpty(json)) return null;
        return JsonSerializer.Deserialize<SessionState>(json);
    }

    public async Task<List<SessionSummary>> GetAllSessionsAsync()
    {
        await EnsureInitializedAsync();
        var json = await _jsRuntime.InvokeAsync<string>("indexedDbStorage.getAllSessions");
        return JsonSerializer.Deserialize<List<SessionSummary>>(json) ?? new List<SessionSummary>();
    }

    public async Task DeleteSessionAsync(string id)
    {
        await EnsureInitializedAsync();
        await _jsRuntime.InvokeVoidAsync("indexedDbStorage.deleteSession", id);
    }

    public async Task<string> SaveFileAsync(string sessionId, string fileName, string csvText)
    {
        await EnsureInitializedAsync();
        return await _jsRuntime.InvokeAsync<string>("indexedDbStorage.saveFile", sessionId, fileName, csvText);
    }

    public async Task<SessionState?> LoadSessionWithFileAsync(string id)
    {
        await EnsureInitializedAsync();
        var json = await _jsRuntime.InvokeAsync<string>("indexedDbStorage.loadSessionWithFile", id);
        if (string.IsNullOrEmpty(json)) return null;
        return JsonSerializer.Deserialize<SessionState>(json);
    }
}

public class SessionSummary
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? FileName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastModified { get; set; }
    public int DataRowCount { get; set; }
}
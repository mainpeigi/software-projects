using Supabase;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using Microsoft.JSInterop;
using System.Net.Http.Headers;

namespace Datemulte_2.Services;

public class SupabaseAuthService
{
    private readonly Supabase.Client _supabaseClient;
    private readonly IJSRuntime _jsRuntime;
    private readonly HttpClient _httpClient;
    private readonly string _supabaseUrl;
    private readonly string _serviceRoleKey;
    private User? _currentUser;
    private bool _initialized = false;

    public event Action? OnAuthStateChanged;

    public SupabaseAuthService(IConfiguration configuration, IJSRuntime jsRuntime)
    {
        _supabaseUrl = configuration["Supabase:Url"]!;
        var key = configuration["Supabase:Key"]!;
        _serviceRoleKey = configuration["Supabase:ServiceRoleKey"] ?? "";

        _supabaseClient = new Supabase.Client(_supabaseUrl, key);
        _jsRuntime = jsRuntime;
        _httpClient = new HttpClient();
    }

    public async Task InitializedAsync()
    {
        if(_initialized) return;

        try
        {
            var session = await _jsRuntime.InvokeAsync<SessionData>("authStorage.getSession");

            if(!string.IsNullOrEmpty(session?.AccessToken) && !string.IsNullOrEmpty(session?.RefreshToken))
            {
                var restoredSession = await _supabaseClient.Auth.SetSession(session.AccessToken, session.RefreshToken);
                if(restoredSession?.User != null)
                {
                    _currentUser = restoredSession.User;
                    OnAuthStateChanged?.Invoke();
                }
            }
        }
        catch
        { }
        _initialized = true;
    }

    public User? CurrentUser => _currentUser;
    public bool IsAuthenticated => _currentUser != null;
    public Guid? CurrentUserId => _currentUser != null ? Guid.Parse(_currentUser.Id!) : null;

    public async Task<(bool success, string? error)> SignUpAsync(string email, string password)
    {
        try
        {
            var response = await _supabaseClient.Auth.SignUp(email, password);

            if(response?.User != null)
            {
                _currentUser = response.User;
                await SaveSessionAsync();
                OnAuthStateChanged?.Invoke();
                return (true, null);
            }

            return (false, "Sign up failed");
        }
        catch(Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool success, string? error)> SignInAsync(string email, string password)
    {
        try
        {
            var response = await _supabaseClient.Auth.SignIn(email, password);

            if(response?.User != null)
            {
                _currentUser = response.User;
                await SaveSessionAsync();
                OnAuthStateChanged?.Invoke();
                return (true, null);
            }

            return (false, "Invalid email or password");
        }
        catch(Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task SignOutAsync()
    {
        await _supabaseClient.Auth.SignOut();
        _currentUser = null;

        try
        {
            await _jsRuntime.InvokeVoidAsync("authStorage.clearSession");
        }
        catch { }
        OnAuthStateChanged?.Invoke();
    }

    public string? GetUserEmail() => _currentUser?.Email;

    public string GetUserInitials()
    {
        var email = _currentUser?.Email;
        if(string.IsNullOrEmpty(email)) return "?";
        return email.Substring(0, Math.Min(2, email.Length)).ToUpper();
    }

    private async Task SaveSessionAsync()
    {
        try
        {
            var session = _supabaseClient.Auth.CurrentSession;
            if(session != null)
            {
                await _jsRuntime.InvokeVoidAsync("authStorage.setSession", session.AccessToken, session.RefreshToken);
            }
        }
        catch { }
    }

    private class SessionData
    {
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
    }

    public async Task<(bool success, string? error)> ChangePasswordAsync(string newPassword)
    {
        try
        {
            var response = await _supabaseClient.Auth.Update(new Supabase.Gotrue.UserAttributes
            {
                Password = newPassword
            });

            if (response != null)
            {
                return (true, null);
            }
            return (false, "Failed to update password");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool success, string? error)> VerifyPasswordAsync(string email, string password)
    {
        try
        {
            var response = await _supabaseClient.Auth.SignIn(email, password);
            return (response?.User != null, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool success, string? error)> DeleteAccountAsync(Guid userId)
    {
        try
        {
            if (string.IsNullOrEmpty(_serviceRoleKey))
            {
                await ClearLocalSessionAsync();
                return (false, "Service role key not configured - user data deleted but auth account remains");
            }

            var request = new HttpRequestMessage(HttpMethod.Delete, $"{_supabaseUrl}/auth/v1/admin/users/{userId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceRoleKey);
            request.Headers.Add("apikey", _serviceRoleKey);

            var response = await _httpClient.SendAsync(request);

            await ClearLocalSessionAsync();

            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            return (false, $"Failed to delete auth account: {errorContent}");
        }
        catch (Exception ex)
        {
            await ClearLocalSessionAsync();
            return (false, ex.Message);
        }
    }

    private async Task ClearLocalSessionAsync()
    {
        _currentUser = null;
        try
        {
            await _jsRuntime.InvokeVoidAsync("authStorage.clearSession");
        }
        catch { }
        OnAuthStateChanged?.Invoke();
    }

    public string? GetAccountCreatedAt()
    {
        try
        {
            var createdAt = _currentUser?.CreatedAt;
            return createdAt?.ToString("MMM dd, yyyy");
        }
        catch
        {
            return null;
        }
    }
}

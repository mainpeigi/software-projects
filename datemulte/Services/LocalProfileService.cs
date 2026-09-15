namespace Datemulte_2.Services;

/// <summary>Local file ownership and preferences, not a cloud authentication provider.</summary>
public class LocalProfileService
{
    public static readonly Guid LocalUserId = Guid.Parse("343d5739-72c3-41f7-a4ba-a5d70b3e7226");
    public record LocalUser(string Id, string Email);
    public LocalUser CurrentUser { get; } = new(LocalUserId.ToString(), "local@localhost");
    public bool IsLocalMode => true;
    public bool IsAuthenticated => false;
    public Guid? CurrentUserId => LocalUserId;
    public event Action? OnAuthStateChanged;
    public Task InitializedAsync() => Task.CompletedTask;
    public string? GetUserEmail() => CurrentUser.Email;
    public string GetUserInitials() => "LO";
    public string? GetAccountCreatedAt() => null;
    public Task SignOutAsync() { OnAuthStateChanged?.Invoke(); return Task.CompletedTask; }
    private static Task<(bool success, string? error)> CloudUnavailable() =>
        Task.FromResult<(bool, string?)>((false, "Cloud accounts are unavailable in the local edition."));
    public Task<(bool success, string? error)> SignUpAsync(string email, string password) => CloudUnavailable();
    public Task<(bool success, string? error)> SignInAsync(string email, string password) => CloudUnavailable();
    public Task<(bool success, string? error)> ChangePasswordAsync(string password) => CloudUnavailable();
    public Task<(bool success, string? error)> VerifyPasswordAsync(string email, string password) => CloudUnavailable();
    public Task<(bool success, string? error)> DeleteAccountAsync(Guid userId) => CloudUnavailable();
}

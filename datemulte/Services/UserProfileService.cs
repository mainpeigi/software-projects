using Datemulte_2.Data;
using Datemulte_2.Models;
using Datemulte_2.Services.DataManagement;
using Microsoft.EntityFrameworkCore;
using Supabase.Gotrue;

namespace Datemulte_2.Services;

/// <summary>
/// Service for managing user profile CRUD operations and related data
/// Uses IDbContextFactory pattern for thread-safe EF Core usage in Blazor Server
/// Provides methods for profile management, 2FA configuration, and data deletion
/// </summary>
public class UserProfileService
{
    // DbContext factory for creating short-lived contexts per operation
    private readonly IDbContextFactory<DataManagementDbContext> _dbContextFactory;

    // Event triggered when profile data changes (for UI refresh)
    public event Action? OnProfileChanged;

    public UserProfileService(IDbContextFactory<DataManagementDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    /// <summary>
    /// Notifies subscribers that profile data has changed
    /// Used to trigger UI updates across components
    /// </summary>
    public void NotifyProfileChanged() => OnProfileChanged?.Invoke();

    /// <summary>
    /// Retrieves user profile by user ID
    /// </summary>
    /// <param name="userId">Supabase auth user ID</param>
    /// <returns>UserProfile if found, null otherwise</returns>
    public async Task<UserProfile?> GetProfileAsync(Guid userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        return await dbContext.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
    }

    /// <summary>
    /// Gets existing profile or creates a new one if it doesn't exist
    /// Initializes display name from email if provided
    /// </summary>
    /// <param name="userId">Supabase auth user ID</param>
    /// <param name="email">User's email address (optional)</param>
    /// <returns>Existing or newly created UserProfile</returns>
    public async Task<UserProfile> GetOrCreateProfileAsync(Guid userId, string? email = null)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var profile = await dbContext.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);

        // Create new profile if not found
        if(profile == null)
        {
            profile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                DisplayName = email?.Split('@')[0],  // Extract username from email
                Email = email,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.UserProfiles.Add(profile);
            await dbContext.SaveChangesAsync();
        }
        else if (email != null && profile.Email != email)
        {
            // Sync email if changed
            profile.Email = email;
            profile.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();
        }
        return profile;
    }

    /// <summary>
    /// Updates user profile display name and avatar, or creates profile if it doesn't exist
    /// </summary>
    /// <param name="userId">Supabase auth user ID</param>
    /// <param name="displayName">New display name</param>
    /// <param name="avatarUrl">New avatar URL (optional)</param>
    /// <returns>Updated or newly created UserProfile</returns>
    public async Task<UserProfile> UpdateProfileAsync(Guid userId, string? displayName, string? avatarUrl = null)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var profile = await dbContext.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);

        // Create new profile if not found
        if (profile == null)
        {
            profile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                DisplayName = displayName,
                AvatarUrl = avatarUrl,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.UserProfiles.Add(profile);
        }
        // Update existing profile
        else
        {
            profile.DisplayName = displayName;
            profile.AvatarUrl = avatarUrl;
            profile.UpdatedAt = DateTime.UtcNow;
        }
        await dbContext.SaveChangesAsync();
        return profile;
    }

    /// <summary>
    /// Gets the count of data sessions for a specific user
    /// Used for profile statistics display
    /// </summary>
    /// <param name="userId">Supabase auth user ID</param>
    /// <returns>Number of sessions owned by the user</returns>
    public async Task<int> GetSessionCountAsync(Guid userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        return await dbContext.DataSessions.CountAsync(s => s.UserId == userId);
    }

    /// <summary>
    /// Updates user's email notification preferences
    /// </summary>
    /// <param name="userId">Supabase auth user ID</param>
    /// <param name="emailNotifications">Enable or disable email notifications</param>
    public async Task UpdateNotificationPreferencesAsync(Guid userId, bool emailNotifications)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var profile = await dbContext.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile != null)
        {
            profile.EmailNotifications = emailNotifications;
            profile.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Permanently deletes all user data including profile, sessions, and related entities
    /// Performs cascade deletion in correct order to maintain referential integrity
    /// WARNING: This operation cannot be undone
    /// </summary>
    /// <param name="userId">Supabase auth user ID</param>
    public async Task DeleteUserDataAsync(Guid userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        // Get all user sessions
        var sessions = await dbContext.DataSessions
            .Where(s => s.UserId == userId)
            .ToListAsync();

        var sessionIds = sessions.Select(s => s.SessionId).ToList();

        // Delete UI states (child of sessions)
        var uiStates = await dbContext.SessionUIStates
            .Where(u => sessionIds.Contains(u.SessionId))
            .ToListAsync();
        dbContext.SessionUIStates.RemoveRange(uiStates);

        // Delete data snapshots (child of sessions)
        var snapshots = await dbContext.DataSnapshots
            .Where(s => sessionIds.Contains(s.SessionId))
            .ToListAsync();
        dbContext.DataSnapshots.RemoveRange(snapshots);

        // Delete change events (child of sessions)
        var changeEvents = await dbContext.DataChangeEvents
            .Where(c => sessionIds.Contains(c.SessionId))
            .ToListAsync();
        dbContext.DataChangeEvents.RemoveRange(changeEvents);

        // Collect source file IDs for later deletion
        var sourceFileIds = sessions
            .Where(s => s.SourceFileId.HasValue)
            .Select(s => s.SourceFileId!.Value)
            .Distinct()
            .ToList();

        // Delete sessions
        dbContext.DataSessions.RemoveRange(sessions);

        // Delete source files if any exist
        if (sourceFileIds.Any())
        {
            var sourceFiles = await dbContext.SourceFiles
                .Where(f => sourceFileIds.Contains(f.FileId))
                .ToListAsync();
            dbContext.SourceFiles.RemoveRange(sourceFiles);
        }

        // Finally, delete the user profile
        var profile = await dbContext.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile != null)
        {
            dbContext.UserProfiles.Remove(profile);
        }

        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Updates user's avatar URL
    /// </summary>
    /// <param name="userId">Supabase auth user ID</param>
    /// <param name="avatarUrl">New avatar URL</param>
    /// <returns>Updated avatar URL, or null if profile not found</returns>
    public async Task<string?> UpdateAvatarAsync(Guid userId, string avatarUrl)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var profile = await dbContext.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile != null)
        {
            profile.AvatarUrl = avatarUrl;
            profile.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();
            return profile.AvatarUrl;
        }
        return null;
    }

    /// <summary>
    /// Retrieves the user's TOTP secret for 2FA
    /// </summary>
    /// <param name="userId">Supabase auth user ID</param>
    /// <returns>Base32-encoded TOTP secret, or null if not configured</returns>
    public async Task<string?> GetTwoFactorSecretAsync(Guid userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var profile = await dbContext.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        return profile?.TwoFactorSecret;
    }

    /// <summary>
    /// Updates user's two-factor authentication configuration
    /// Sets both the enabled flag and TOTP secret
    /// </summary>
    /// <param name="userId">Supabase auth user ID</param>
    /// <param name="enabled">Enable or disable 2FA</param>
    /// <param name="secret">Base32-encoded TOTP secret (null to disable)</param>
    public async Task UpdateTwoFactorAsync(Guid userId, bool enabled, string? secret)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var profile = await dbContext.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile != null)
        {
            profile.TwoFactorEnabled = enabled;
            profile.TwoFactorSecret = secret;
            profile.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();
        }
    }
        /// <summary>
    /// Updates extended profile fields (bio, pronouns, timezone, language)
    /// </summary>
    public async Task<(bool success, string message)> UpdateExtendedProfileAsync(
        Guid userId, 
        string? bio, 
        string? pronouns, 
        string? timezone, 
        string? language)
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            
            var profile = await dbContext.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
                return (false, "Profile not found");

            // Validate bio length
            if (!string.IsNullOrWhiteSpace(bio) && bio.Length > 500)
                return (false, "Bio must be 500 characters or less");

            profile.Bio = bio;
            profile.Pronouns = pronouns;
            profile.Timezone = timezone;
            profile.Language = language ?? "en-US";
            profile.UpdatedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync();

            OnProfileChanged?.Invoke();
            return (true, "Extended profile updated successfully");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to update extended profile: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates accessibility settings
    /// </summary>
    public async Task<(bool success, string message)> UpdateAccessibilitySettingsAsync(
        Guid userId,
        bool reducedMotion,
        bool screenReaderOptimized,
        int fontSizeMultiplier)
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            
            var profile = await dbContext.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
                return (false, "Profile not found");

            // Validate font size multiplier
            if (fontSizeMultiplier < 50 || fontSizeMultiplier > 200)
                return (false, "Font size multiplier must be between 50% and 200%");

            profile.ReducedMotion = reducedMotion;
            profile.ScreenReaderOptimized = screenReaderOptimized;
            profile.FontSizeMultiplier = fontSizeMultiplier;
            profile.UpdatedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync();

            OnProfileChanged?.Invoke();
            return (true, "Accessibility settings updated successfully");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to update accessibility settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets complete profile including all new fields
    /// </summary>
    public async Task<UserProfile?> GetCompleteProfileAsync(Guid userId)
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            
            var profile = await dbContext.UserProfiles
                .Include(p => p.Theme)
                .FirstOrDefaultAsync(p => p.UserId == userId);

            return profile;
        }
        catch
        {
            return null;
        }
    }

}
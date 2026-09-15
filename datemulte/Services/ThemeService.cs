using Datemulte_2.Data;
using Datemulte_2.Models;
using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace Datemulte_2.Services;

/// <summary>
/// Manages user theme configurations and provides runtime theme switching
/// </summary>
public class ThemeService
{
    private readonly IDbContextFactory<DataManagementDbContext> _dbContextFactory;

    public ThemeService(IDbContextFactory<DataManagementDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    /// <summary>
    /// Gets the user's theme configuration or creates default if none exists
    /// </summary>
    public async Task<ThemeConfiguration> GetOrCreateThemeAsync(Guid userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var theme = await dbContext.ThemeConfigurations
            .FirstOrDefaultAsync(t => t.UserId == userId);

        if (theme == null)
        {
            theme = new ThemeConfiguration
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ThemeName = "Default Dark",
                PrimaryColor = "#20818e",
                SecondaryColor = "#2dd4bf",
                BackgroundColor = "#0a0e1a",
                SurfaceColor = "#1a2332",
                TextPrimaryColor = "#f1f5f9",
                TextSecondaryColor = "#94a3b8",
                FontFamily = "Roboto, sans-serif",
                BaseFontSize = 14,
                HighContrast = false,
                ColorBlindMode = "None"
            };

            dbContext.ThemeConfigurations.Add(theme);
            await dbContext.SaveChangesAsync();
        }

        return theme;
    }

    /// <summary>
    /// Updates user's theme configuration
    /// </summary>
    public async Task<bool> UpdateThemeAsync(Guid userId, ThemeConfiguration updatedTheme)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var theme = await dbContext.ThemeConfigurations
            .FirstOrDefaultAsync(t => t.UserId == userId);

        if (theme == null)
            return false;

        theme.ThemeName = updatedTheme.ThemeName;
        theme.PrimaryColor = updatedTheme.PrimaryColor;
        theme.SecondaryColor = updatedTheme.SecondaryColor;
        theme.BackgroundColor = updatedTheme.BackgroundColor;
        theme.SurfaceColor = updatedTheme.SurfaceColor;
        theme.TextPrimaryColor = updatedTheme.TextPrimaryColor;
        theme.TextSecondaryColor = updatedTheme.TextSecondaryColor;
        theme.FontFamily = updatedTheme.FontFamily;
        theme.BaseFontSize = updatedTheme.BaseFontSize;
        theme.HighContrast = updatedTheme.HighContrast;
        theme.ColorBlindMode = updatedTheme.ColorBlindMode;
        theme.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Generates a MudBlazor theme from user's theme configuration
    /// </summary>
    public MudTheme GenerateMudTheme(ThemeConfiguration config)
    {
        var theme = new MudTheme
        {
            PaletteDark = new PaletteDark
            {
                Primary = config.PrimaryColor,
                Secondary = config.SecondaryColor,
                Background = config.BackgroundColor,
                Surface = config.SurfaceColor,
                TextPrimary = config.TextPrimaryColor,
                TextSecondary = config.TextSecondaryColor,
                AppbarBackground = config.SurfaceColor,
                DrawerBackground = config.SurfaceColor,
                DrawerText = config.TextPrimaryColor,
                DrawerIcon = config.TextSecondaryColor
            }
        };

        return theme;
    }

    /// <summary>
    /// Gets preset theme configurations
    /// </summary>
    public List<ThemeConfiguration> GetPresetThemes()
    {
        return new List<ThemeConfiguration>
        {
            new ThemeConfiguration
            {
                ThemeName = "Default Dark",
                PrimaryColor = "#20818e",
                SecondaryColor = "#2dd4bf",
                BackgroundColor = "#0a0e1a",
                SurfaceColor = "#1a2332",
                TextPrimaryColor = "#f1f5f9",
                TextSecondaryColor = "#94a3b8"
            },
            new ThemeConfiguration
            {
                ThemeName = "Ocean Blue",
                PrimaryColor = "#0ea5e9",
                SecondaryColor = "#38bdf8",
                BackgroundColor = "#0c1222",
                SurfaceColor = "#1e293b",
                TextPrimaryColor = "#f1f5f9",
                TextSecondaryColor = "#94a3b8"
            },
            new ThemeConfiguration
            {
                ThemeName = "Forest Green",
                PrimaryColor = "#10b981",
                SecondaryColor = "#34d399",
                BackgroundColor = "#0a1410",
                SurfaceColor = "#1a2e23",
                TextPrimaryColor = "#f1f5f9",
                TextSecondaryColor = "#94a3b8"
            },
            new ThemeConfiguration
            {
                ThemeName = "High Contrast",
                PrimaryColor = "#ffffff",
                SecondaryColor = "#ffff00",
                BackgroundColor = "#000000",
                SurfaceColor = "#1a1a1a",
                TextPrimaryColor = "#ffffff",
                TextSecondaryColor = "#ffff00",
                HighContrast = true
            }
        };
    }

    /// <summary>
    /// Deletes user's theme configuration (reverts to default)
    /// </summary>
    public async Task<bool> DeleteThemeAsync(Guid userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var theme = await dbContext.ThemeConfigurations
            .FirstOrDefaultAsync(t => t.UserId == userId);

        if (theme == null)
            return false;

        dbContext.ThemeConfigurations.Remove(theme);
        await dbContext.SaveChangesAsync();
        return true;
    }
}

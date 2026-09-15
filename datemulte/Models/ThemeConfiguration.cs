namespace Datemulte_2.Models;

/// <summary>
/// User-specific theme customization settings
/// Allows runtime theme switching and custom color schemes
/// </summary>
public class ThemeConfiguration
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }  // FK to UserProfile
    
    public string ThemeName { get; set; } = "Default";
    
    // Color palette
    public string PrimaryColor { get; set; } = "#20818e";
    public string SecondaryColor { get; set; } = "#2dd4bf";
    public string BackgroundColor { get; set; } = "#0a0e1a";
    public string SurfaceColor { get; set; } = "#1a2332";
    public string TextPrimaryColor { get; set; } = "#f1f5f9";
    public string TextSecondaryColor { get; set; } = "#94a3b8";
    
    // Typography
    public string FontFamily { get; set; } = "Roboto, sans-serif";
    public int BaseFontSize { get; set; } = 14;
    
    // Accessibility
    public bool HighContrast { get; set; } = false;
    public string ColorBlindMode { get; set; } = "None"; // None, Protanopia, Deuteranopia, Tritanopia
    
    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation property
    public UserProfile User { get; set; } = null!;
}

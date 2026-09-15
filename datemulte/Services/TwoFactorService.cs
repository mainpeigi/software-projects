using OtpNet;
using System.Security.Cryptography;

namespace Datemulte_2.Services;

/// <summary>
/// Service for two-factor authentication using TOTP (Time-based One-Time Password)
/// Implements RFC 6238 standard for generating and validating 6-digit authentication codes
/// Registered as singleton in DI container for consistent TOTP handling
/// </summary>
public class TwoFactorService
{
    // Application name shown in authenticator apps
    private const string Issuer = "Datemulte";

    /// <summary>
    /// Generates a new TOTP secret and QR code URL for 2FA setup
    /// </summary>
    /// <param name="userEmail">User's email to identify the account in authenticator apps</param>
    /// <returns>Tuple containing (Base32-encoded secret, QR code image URL)</returns>
    public (string secret, string qrCodeUrl) GenerateSetupInfo(string userEmail)
    {
        // Generate cryptographically secure random 20-byte secret
        var secret = Base32Encoding.ToString(RandomNumberGenerator.GetBytes(20));

        // Create otpauth URI for authenticator apps (Google Authenticator, Authy, etc.)
        var otpUri = $"otpauth://totp/{Issuer}:{userEmail}?secret={secret}&issuer={Issuer}&digits=6";

        // Generate QR code URL using external API
        var qrCodeUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=200x200&data={Uri.EscapeDataString(otpUri)}";

        return (secret, qrCodeUrl);
    }

    /// <summary>
    /// Validates a 6-digit TOTP code against the user's secret
    /// Uses a time window of ±30 seconds to account for clock drift
    /// </summary>
    /// <param name="secret">Base32-encoded TOTP secret</param>
    /// <param name="code">6-digit code from authenticator app</param>
    /// <returns>True if code is valid, false otherwise</returns>
    public bool ValidateCode(string secret, string code)
    {
        // Validate inputs
        if(string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(code))
        {
            return false;
        }

        // Create TOTP generator from stored secret
        var totp = new Totp(Base32Encoding.ToBytes(secret));

        // Verify code with ±1 time step (±30 seconds) tolerance
        return totp.VerifyTotp(code, out _, new VerificationWindow(1, 1));
    }
}
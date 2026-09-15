namespace Datemulte_2.Middleware;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // X-Frame-Options: Prevents clickjacking by disallowing the site to be embedded in iframes
        context.Response.Headers.Append("X-Frame-Options", "DENY");

        // X-Content-Type-Options: Prevents MIME-sniffing attacks
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");

        // X-XSS-Protection: Enables browser's XSS filter (legacy, but still useful for older browsers)
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");

        // Referrer-Policy: Controls how much referrer information is sent
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

        // Strict-Transport-Security (HSTS): Forces HTTPS connections
        context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");

        // Content-Security-Policy: Defines approved sources of content
        // Note: 'unsafe-inline' and 'unsafe-eval' are needed for Blazor and MudBlazor to work
        // cdn.jsdelivr.net is needed for ECharts library
        context.Response.Headers.Append("Content-Security-Policy",
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net; " +
            "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
            "font-src 'self' https://fonts.gstatic.com; " +
            "img-src 'self' data: https:; " +
            "connect-src 'self' https://*.supabase.co wss://*;");

        // Permissions-Policy: Controls which browser features can be used
        context.Response.Headers.Append("Permissions-Policy", 
            "geolocation=(), microphone=(), camera=()");

        await _next(context);
    }
}
using System.Text.Json;

namespace Datemulte_2.Middleware;

/// <summary>
/// Global error handling middleware
/// Catches unhandled exceptions and returns sanitized error responses
/// </summary>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Log the full exception details internally
        _logger.LogError(exception, "An unhandled exception occurred: {Message}", exception.Message);

        // Return a sanitized error response to the client
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var response = new
        {
            error = "An error occurred while processing your request.",
            // Only include exception type in development mode
            type = context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment() 
                ? exception.GetType().Name 
                : null
        };

        var jsonResponse = JsonSerializer.Serialize(response);
        await context.Response.WriteAsync(jsonResponse);
    }
}
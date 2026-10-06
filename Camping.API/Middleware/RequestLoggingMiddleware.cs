namespace Camping.API.Middleware;

/// <summary>
/// Writes one <c>Information</c> log line for every incoming request, tagged with
/// the request's correlation id (Step 13).
///
/// Only the HTTP method and the path are logged - never request headers and
/// never the body - so an <c>Authorization</c> header (a JWT) or a password in a
/// request body is never written to the log.
/// </summary>
public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        _logger.LogInformation(
            "Incoming request {Method} {Path} (correlationId: {CorrelationId})",
            context.Request.Method,
            context.Request.Path.Value,
            CorrelationIdMiddleware.GetCorrelationId(context));

        await _next(context);
    }
}

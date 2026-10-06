namespace Camping.API.Middleware;

/// <summary>
/// Gives every request a correlation id so a single call can be followed from the
/// caller's request, through the response, and (later) through the logs.
///
/// The id is taken from the inbound <c>X-Correlation-ID</c> header when the
/// caller supplied a sane value, otherwise a fresh GUID is generated. It is then
///   * stored on <see cref="HttpContext.Items"/> (read via <see cref="GetCorrelationId"/>),
///   * copied to <see cref="HttpContext.TraceIdentifier"/>, and
///   * written back on the response as <c>X-Correlation-ID</c>.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";
    internal const string ItemKey = "CorrelationId";
    private const int MaxLength = 128;

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = Resolve(context);

        context.Items[ItemKey] = correlationId;
        context.TraceIdentifier = correlationId;

        // Set on the way out, once, whoever writes the response first.
        context.Response.OnStarting(state =>
        {
            var ctx = (HttpContext)state;
            ctx.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        }, context);

        await _next(context);
    }

    /// <summary>The correlation id assigned to the current request.</summary>
    public static string GetCorrelationId(HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var value) && value is string id
            ? id
            : context.TraceIdentifier;

    private static string Resolve(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var supplied))
        {
            var candidate = supplied.ToString().Trim();
            if (candidate.Length is > 0 and <= MaxLength)
            {
                return candidate;
            }
        }

        return Guid.NewGuid().ToString("D");
    }
}

using System.Text.Json;
using Camping.Core.Exceptions;
using Camping.Service.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Camping.API.Middleware;

/// <summary>
/// Single place where an exception escaping the pipeline becomes an HTTP
/// response. Domain exceptions map to their status code; anything else is a 500
/// with a generic message so implementation detail is never leaked. The body is
/// always RFC 7807 <c>application/problem+json</c> and always carries the
/// request's correlation id, so every error the API returns has the same shape.
///
/// Every handled exception is also logged (Step 13): a conflict over the limited
/// resource (a lost optimistic-concurrency race, or a tent that is already
/// booked) is a normal, recoverable outcome and is logged as <c>Warning</c>;
/// everything else is logged as <c>Error</c>. Only the exception itself and the
/// request method/path are logged - never headers or the body.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client went away and the CancellationToken stopped the work.
            // Not a failure: nobody is waiting for a response any more.
            _logger.LogInformation(
                "Request {Method} {Path} was cancelled by the client (correlationId: {CorrelationId})",
                context.Request.Method, context.Request.Path.Value,
                CorrelationIdMiddleware.GetCorrelationId(context));
        }
        catch (Exception ex)
        {
            Log(context, ex);
            await WriteProblemAsync(context, ex);
        }
    }

    private void Log(HttpContext context, Exception ex)
    {
        var method = context.Request.Method;
        var path = context.Request.Path.Value;
        var correlationId = CorrelationIdMiddleware.GetCorrelationId(context);

        if (ex is ConcurrencyConflictException or ResourceConflictException)
        {
            // A clash over the limited resource (a tent) - expected and handled.
            _logger.LogWarning(
                "Resource conflict on {Method} {Path}: {Reason} (correlationId: {CorrelationId})",
                method, path, ex.Message, correlationId);
        }
        else
        {
            _logger.LogError(ex,
                "Handled exception on {Method} {Path} (correlationId: {CorrelationId})",
                method, path, correlationId);
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, Exception ex)
    {
        var (status, title, detail) = Map(ex);
        var correlationId = CorrelationIdMiddleware.GetCorrelationId(context);

        if (context.Response.HasStarted)
        {
            // Too late to change the response - let it fail rather than corrupt the stream.
            return;
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["correlationId"] = correlationId;

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        context.Response.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problem, SerializerOptions));
    }

    private static (int Status, string Title, string? Detail) Map(Exception ex) => ex switch
    {
        NotFoundException => (StatusCodes.Status404NotFound, "Not Found", ex.Message),
        BusinessRuleException => (StatusCodes.Status400BadRequest, "Bad Request", ex.Message),
        AuthenticationFailedException => (StatusCodes.Status401Unauthorized, "Unauthorized", ex.Message),
        ConcurrencyConflictException => (StatusCodes.Status409Conflict, "Conflict", ex.Message),
        ResourceConflictException => (StatusCodes.Status409Conflict, "Conflict", ex.Message),
        _ => (StatusCodes.Status500InternalServerError, "Internal Server Error",
              "An unexpected error occurred while processing the request.")
    };
}

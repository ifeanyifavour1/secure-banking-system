using System.Security.Claims;
using System.Text.Json;
using BankingApi.Data;
using BankingApi.Models;

namespace BankingApi.Middleware;

public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLoggingMiddleware> _logger;

    public AuditLoggingMiddleware(RequestDelegate next, ILogger<AuditLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IServiceScopeFactory scopeFactory)
    {
        await _next(context);

        if (ShouldSkip(context))
        {
            return;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();

            var userId = ResolveUserId(context);
            var action = MapAction(context.Request.Method);
            var path = context.Request.Path.Value ?? "/";

            db.AuditLogEntries.Add(new AuditLogEntry
            {
                EventType = "api_access",
                EntityType = "endpoint",
                EntityId = path,
                Action = action,
                PerformedBy = userId,
                IpAddress = context.Connection.RemoteIpAddress,
                UserAgent = context.Request.Headers.UserAgent.ToString(),
                AdditionalInfo = JsonSerializer.SerializeToDocument(new
                {
                    method = context.Request.Method,
                    statusCode = context.Response.StatusCode
                }),
                CreatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write API access audit log for {Path}", context.Request.Path);
        }
    }

    private static bool ShouldSkip(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        return path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase);
    }

    private static Guid? ResolveUserId(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var subject = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");

        return Guid.TryParse(subject, out var userId) ? userId : null;
    }

    private static string MapAction(string method) => method.ToUpperInvariant() switch
    {
        "GET" => "read",
        "POST" => "create",
        "PUT" or "PATCH" => "update",
        "DELETE" => "delete",
        _ => "read"
    };
}

public static class AuditLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseAuditLogging(this IApplicationBuilder app)
    {
        return app.UseMiddleware<AuditLoggingMiddleware>();
    }
}

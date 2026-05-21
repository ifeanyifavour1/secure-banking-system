using System.Net.Sockets;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BankingApi.Middleware;

public class DatabaseExceptionHandler : IExceptionHandler
{
    private readonly ILogger<DatabaseExceptionHandler> _logger;

    public DatabaseExceptionHandler(ILogger<DatabaseExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (!IsDatabaseConnectivityFailure(exception))
        {
            return false;
        }

        _logger.LogError(exception, "Database connectivity failure");

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = "Database unavailable",
            Detail =
                "Cannot reach the database server. Check your internet connection and VPN/DNS, "
                + "confirm ConnectionStrings__neondb in project/.env matches your Neon dashboard, "
                + "restart the API, then open http://localhost:5285/health/db."
        };

        httpContext.Response.StatusCode = problem.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(
            new { message = problem.Detail },
            cancellationToken);

        return true;
    }

    private static bool IsDatabaseConnectivityFailure(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is SocketException { SocketErrorCode: SocketError.HostNotFound })
            {
                return true;
            }
        }

        return false;
    }
}

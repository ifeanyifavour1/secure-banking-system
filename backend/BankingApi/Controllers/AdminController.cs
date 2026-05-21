using System.Net;
using System.Security.Claims;
using System.Text.Json;
using BankingApi.Auth;
using BankingApi.Data;
using BankingApi.DTOs;
using BankingApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BankingApi.Controllers;

/// <summary>
/// Internal admin operations. Requires admin JWT and X-Admin-Secret for role changes.
/// </summary>
[ApiController]
[Route("api/internal/staff")]
[Authorize(Policy = "AdminOnly")]
[Tags("Admin")]
public class AdminController : ControllerBase
{
    public const string AdminSecretHeaderName = "X-Admin-Secret";

    /// <summary>Roles that may be assigned via this API. Admin is never assignable (use DB seeds only).</summary>
    private static readonly HashSet<string> AssignableRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "customer",
        "teller",
        "manager"
    };

    private static readonly HashSet<string> ProtectedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin"
    };

    private readonly BankingDbContext _db;
    private readonly AdminSettings _adminSettings;

    public AdminController(BankingDbContext db, IOptions<AdminSettings> adminSettings)
    {
        _db = db;
        _adminSettings = adminSettings.Value;
    }

    /// <summary>
    /// Assign RBAC role to a user (customer, teller, or manager only). Admin JWT + X-Admin-Secret required.
    /// The admin role cannot be granted through this endpoint.
    /// </summary>
    [HttpPost("role")]
    public async Task<ActionResult<SetUserRoleResponse>> SetUserRole(SetUserRoleRequest request)
    {
        if (!ValidateAdminSecret())
        {
            return Unauthorized(new { message = "Invalid or missing admin secret." });
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!TryGetCurrentUserId(out var adminUserId))
        {
            return Unauthorized(new { message = "Invalid or missing authentication token." });
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var newRole = request.Role.Trim().ToLowerInvariant();

        if (ProtectedRoles.Contains(newRole))
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = "The admin role cannot be assigned through the application. Use database provisioning only." });
        }

        if (!AssignableRoles.Contains(newRole))
        {
            return BadRequest(new { message = "Invalid role." });
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
        if (user is null)
        {
            return NotFound(new { message = "User not found." });
        }

        if (user.UserId == adminUserId && newRole != "admin")
        {
            return BadRequest(new { message = "You cannot remove your own admin role." });
        }

        var previousRole = user.Role;
        if (string.Equals(previousRole, newRole, StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new SetUserRoleResponse
            {
                UserId = user.UserId,
                Email = user.Email,
                PreviousRole = previousRole,
                NewRole = newRole,
                UpdatedAt = user.UpdatedAt
            });
        }

        var now = DateTime.UtcNow;
        user.Role = newRole;
        user.UpdatedAt = now;

        LogRoleChange(user, previousRole, newRole, adminUserId, now);
        await _db.SaveChangesAsync();

        return Ok(new SetUserRoleResponse
        {
            UserId = user.UserId,
            Email = user.Email,
            PreviousRole = previousRole,
            NewRole = newRole,
            UpdatedAt = now
        });
    }

    private bool ValidateAdminSecret()
    {
        var configuredSecret = _adminSettings.RoleAssignmentSecret;
        if (string.IsNullOrWhiteSpace(configuredSecret))
        {
            return false;
        }

        if (!Request.Headers.TryGetValue(AdminSecretHeaderName, out var provided) ||
            string.IsNullOrWhiteSpace(provided))
        {
            return false;
        }

        return string.Equals(
            provided.ToString().Trim(),
            configuredSecret.Trim(),
            StringComparison.Ordinal);
    }

    private void LogRoleChange(
        User user,
        string previousRole,
        string newRole,
        Guid performedBy,
        DateTime changedAt)
    {
        var auditEntry = new AuditLogEntry
        {
            EventType = "authorization",
            EntityType = "user",
            EntityId = user.UserId.ToString(),
            Action = "update",
            PerformedBy = performedBy,
            IpAddress = GetClientIpAddress(),
            UserAgent = Request.Headers.UserAgent.ToString(),
            OldValues = JsonSerializer.SerializeToDocument(new { role = previousRole }),
            NewValues = JsonSerializer.SerializeToDocument(new { role = newRole, user.Email }),
            CreatedAt = changedAt
        };

        _db.AuditLogEntries.Add(auditEntry);
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = default;

        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(subject, out userId);
    }

    private IPAddress? GetClientIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress;
    }
}

using System.Text.Json;
using BankingApi.Data;
using BankingApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankingApi.Controllers
{
    [ApiController]
    [Route("api/audit")]
    [Authorize(Roles = "admin")]
    public class AuditController : ControllerBase
    {
        private readonly BankingDbContext _db;

        private const int DefaultPageSize = 20;
        private const int MaxPageSize = 100;

        public AuditController(BankingDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetAuditLogs(
            [FromQuery] string? entityType,
            [FromQuery] string? entityId,
            [FromQuery] string? action,
            [FromQuery] Guid? performedBy,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = DefaultPageSize,
            CancellationToken cancellationToken = default)
        {
            if (startDate.HasValue && endDate.HasValue && startDate.Value > endDate.Value)
            {
                return BadRequest(new { message = "startDate cannot be after endDate." });
            }

            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? DefaultPageSize : pageSize > MaxPageSize ? MaxPageSize : pageSize;

            IQueryable<AuditLogEntry> query = _db.AuditLogEntries;

            if (!string.IsNullOrWhiteSpace(entityType))
            {
                query = query.Where(a => a.EntityType == entityType.Trim());
            }

            if (!string.IsNullOrWhiteSpace(entityId))
            {
                query = query.Where(a => a.EntityId == entityId.Trim());
            }

            if (!string.IsNullOrWhiteSpace(action))
            {
                var actionFilter = action.Trim().ToLowerInvariant();
                query = query.Where(a => a.Action.ToLower() == actionFilter);
            }

            if (performedBy.HasValue)
            {
                query = query.Where(a => a.PerformedBy == performedBy.Value);
            }

            if (startDate.HasValue)
            {
                query = query.Where(a => a.CreatedAt >= startDate.Value.ToUniversalTime());
            }

            if (endDate.HasValue)
            {
                var exclusiveEndLimit = endDate.Value.ToUniversalTime().AddDays(1);
                query = query.Where(a => a.CreatedAt < exclusiveEndLimit);
            }

            var totalItems = await query.CountAsync(cancellationToken);

            var logs = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => MapToResponse(a))
                .ToListAsync(cancellationToken);

            return Ok(new
            {
                totalItems,
                page,
                pageSize,
                data = logs
            });
        }

        private static AuditLogResponse MapToResponse(AuditLogEntry entry) => new()
        {
            EventType = entry.EventType,
            EntityType = entry.EntityType,
            EntityId = entry.EntityId,
            Action = entry.Action,
            PerformedBy = entry.PerformedBy,
            IpAddress = entry.IpAddress?.ToString() ?? "unknown",
            UserAgent = entry.UserAgent,
            AdditionalInfo = entry.AdditionalInfo,
            OldValues = entry.OldValues,
            NewValues = entry.NewValues,
            CreatedAt = entry.CreatedAt
        };
    }

    #region Data Transfer Objects (DTOs)

    public class AuditLogResponse
    {
        public string EventType { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public Guid? PerformedBy { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public string? UserAgent { get; set; }
        public JsonDocument? AdditionalInfo { get; set; }
        public JsonDocument? OldValues { get; set; }
        public JsonDocument? NewValues { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    #endregion
}

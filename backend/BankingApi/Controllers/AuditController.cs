using BankingApi.Data;
using BankingApi.DTOs;
using BankingApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankingApi.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize(Policy = "AdminOnly")]
public class AuditController : ControllerBase
{
    private readonly BankingDbContext _db;

    public AuditController(BankingDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<AuditLogListResponse>> GetAuditLogs([FromQuery] AuditQuery query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        IQueryable<AuditLogEntry> auditQuery = _db.AuditLogEntries.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            auditQuery = auditQuery.Where(e => e.EntityType == query.EntityType.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.EntityId))
        {
            auditQuery = auditQuery.Where(e => e.EntityId == query.EntityId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            auditQuery = auditQuery.Where(e => e.Action == query.Action.Trim().ToLowerInvariant());
        }

        if (query.PerformedBy.HasValue)
        {
            auditQuery = auditQuery.Where(e => e.PerformedBy == query.PerformedBy);
        }

        if (query.StartDate.HasValue)
        {
            auditQuery = auditQuery.Where(e => e.CreatedAt >= query.StartDate.Value);
        }

        if (query.EndDate.HasValue)
        {
            auditQuery = auditQuery.Where(e => e.CreatedAt <= query.EndDate.Value);
        }

        var totalCount = await auditQuery.CountAsync();

        var entries = await auditQuery
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new AuditLogResponse
            {
                LogId = e.LogId,
                EventType = e.EventType,
                EntityType = e.EntityType,
                EntityId = e.EntityId,
                Action = e.Action,
                PerformedBy = e.PerformedBy,
                CreatedAt = e.CreatedAt
            })
            .ToListAsync();

        return Ok(new AuditLogListResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Entries = entries
        });
    }
}

namespace BankingApi.DTOs;

public class AuditLogResponse
{
    public long LogId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public Guid? PerformedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AuditLogListResponse
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public IReadOnlyList<AuditLogResponse> Entries { get; set; } = Array.Empty<AuditLogResponse>();
}

public class AuditQuery
{
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Action { get; set; }
    public Guid? PerformedBy { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

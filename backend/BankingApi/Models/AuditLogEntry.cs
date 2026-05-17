using System.Net;
using System.Text.Json;

namespace BankingApi.Models
{
    public class AuditLogEntry
    {
        public long LogId { get; set; }

        public string EventType { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;

        public Guid? PerformedBy { get; set; }
        public JsonDocument? OldValues { get; set; }
        public JsonDocument? NewValues { get; set; }
        public IPAddress? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public JsonDocument? AdditionalInfo { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}

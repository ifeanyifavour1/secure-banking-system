using System.Net;

namespace BankingApi.Models
{
    public class Transaction
    {
        public Guid TransactionId { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public string TransactionType { get; set; } = string.Empty;

        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public string? Description { get; set; }

        public int StateId { get; set; }
        public Guid? SourceAccountId { get; set; }
        public Guid? DestAccountId { get; set; }
        public Guid InitiatedBy { get; set; }

        public IPAddress? IpAddress { get; set; }
        public string? Channel { get; set; }

        public DateTime? ScheduledAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class TransactionState
    {
        public int StateId { get; set; }
        public string StateName { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class TransactionEntry
    {
        public long EntryId { get; set; }
        public Guid TransactionId { get; set; }
        public Guid AccountId { get; set; }

        public string EntryType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal BalanceBefore { get; set; }
        public decimal BalanceAfter { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}

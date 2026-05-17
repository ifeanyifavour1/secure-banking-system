namespace BankingApi.Models
{
    public class AccountLimit
    {
        public int LimitId { get; set; }
        public Guid AccountId { get; set; }

        public string LimitType { get; set; } = string.Empty;
        public decimal MaxAmount { get; set; }
        public decimal CurrentUsage { get; set; }
        public DateTime UsageResetAt { get; set; }

        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}

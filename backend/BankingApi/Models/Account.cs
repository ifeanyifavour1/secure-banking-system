namespace BankingApi.Models
{
    public class Account
    {
        public Guid AccountId { get; set; }
        public Guid UserId { get; set; }

        public string AccountNumber { get; set; } = string.Empty;

        public string AccountType { get; set; } = string.Empty;
        public string Currency { get; set; } = "USD";
        
        public decimal Balance { get; set; }
        public decimal AvailableBalance { get; set; }
        public decimal? InterestRate { get; set; }

        public string Status { get; set; } = "active";

        public DateTime OpenedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }


    }
}
using System.ComponentModel.DataAnnotations;

namespace BankingApi.DTOs;

public class CreateAccountRequest
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    [AllowedValues("checking", "savings", "fixed_deposit", "loan", ErrorMessage = "Invalid account type.")]
    public string AccountType { get; set; } = string.Empty;

    [Required]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Currency must be a 3-letter ISO code.")]
    public string Currency { get; set; } = "USD";
}

public class AccountResponse
{
    public Guid AccountId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
    public decimal Balance { get; set; }
    public decimal AvailableBalance { get; set; }
    
    public string Status { get; set; } = "active";
    public DateTime OpenedAt { get; set; }
}

public class AccountListResponse
{
    public List<AccountResponse> Accounts { get; set; } = new();
}
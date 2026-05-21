using System.ComponentModel.DataAnnotations;

namespace BankingApi.DTOs;

public class AccountResponse
{
    public Guid AccountId { get; set; }
    public Guid UserId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal? InterestRate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}

public class AccountListResponse
{
    public IReadOnlyList<AccountResponse> Accounts { get; set; } = Array.Empty<AccountResponse>();
}

public class AccountLookupResponse
{
    public Guid AccountId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class CreateAccountRequest
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    [RegularExpression(
        @"^(checking|savings|fixed_deposit|loan)$",
        ErrorMessage = "Account type must be checking, savings, fixed_deposit, or loan.")]
    public string AccountType { get; set; } = string.Empty;

    [Required]
    [StringLength(3, MinimumLength = 3)]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "Currency must be a 3-letter ISO code (e.g. USD).")]
    public string Currency { get; set; } = "USD";

    [Range(0, 1)]
    public decimal? InterestRate { get; set; }
}

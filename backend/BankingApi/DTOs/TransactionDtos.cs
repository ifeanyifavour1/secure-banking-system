using System.ComponentModel.DataAnnotations;

namespace BankingApi.DTOs;

public class TransferRequest
{
    [Required]
    public Guid SourceAccountId { get; set; }

    [Required]
    public Guid DestAccountId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }
}

public class DepositRequest
{
    [Required]
    public Guid AccountId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }
}

public class WithdrawalRequest
{
    [Required]
    public Guid AccountId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }
}

public class TransactionResponse
{
    public Guid TransactionId { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public Guid? SourceAccountId { get; set; }
    public Guid? DestAccountId { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}

public class TransactionHistoryQuery
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? TransactionType { get; set; }
    public string? State { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

public class TransactionHistoryResponse
{
    public Guid AccountId { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public IReadOnlyList<TransactionResponse> Transactions { get; set; } = Array.Empty<TransactionResponse>();
}

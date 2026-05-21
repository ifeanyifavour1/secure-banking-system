using System.Data;
using System.Security.Claims;
using System.Text.Json;
using BankingApi.Data;
using BankingApi.DTOs;
using BankingApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BankingApi.Controllers;

public enum TransactionStateEnum { Pending = 1, Failed = 2, Completed = 3 }

[ApiController]
[Route("api/transactions")]
[Authorize]
[EnableRateLimiting("transactions")]
public class TransactionsController : ControllerBase
{
    private readonly BankingDbContext _db;
    private readonly ILogger<TransactionsController> _logger;

    public TransactionsController(BankingDbContext db, ILogger<TransactionsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    #region Helpers

    private async Task<string?> CheckLimitAsync(Guid accountId, string limitType, decimal amount)
    {
        var limit = await _db.AccountLimits
            .FirstOrDefaultAsync(l => l.AccountId == accountId && l.LimitType == limitType && l.IsActive);
        
        if (limit != null && (limit.CurrentUsage + amount) > limit.MaxAmount)
            return $"Limit '{limitType}' exceeded.";
        
        return null;
    }

    private async Task CreateAuditLogAsync(string action, string entityId, string details)
    {
        var log = new AuditLogEntry
        {
            EventType = "Transaction",
            EntityType = "Transaction",
            EntityId = entityId,
            Action = action,
            PerformedBy = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString()),
            IpAddress = HttpContext.Connection.RemoteIpAddress,
            CreatedAt = DateTime.UtcNow,
            AdditionalInfo = JsonDocument.Parse(JsonSerializer.Serialize(new { Message = details }))
        };
        _db.AuditLogEntries.Add(log);
    }

    private bool IsTellerOrAbove() => 
        User.IsInRole("teller") || User.IsInRole("manager") || User.IsInRole("admin");

    #endregion

    [HttpPost("transfer")]
    public async Task<IActionResult> Transfer([FromBody] TransferRequest request)
    {
        if (request.SourceAccountId == request.DestAccountId) return BadRequest("Cannot transfer to self.");
        if (request.Amount <= 0) return BadRequest("Amount must be positive.");

        var limitError = await CheckLimitAsync(request.SourceAccountId, "daily_transfer", request.Amount);
        if (limitError != null) return BadRequest(limitError);

        using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var source = await _db.Accounts.FindAsync(request.SourceAccountId);
            var dest = await _db.Accounts.FindAsync(request.DestAccountId);

            if (source == null || dest == null) return NotFound("Accounts not found.");
            
            var currentUserId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            if (source.UserId != currentUserId && !IsTellerOrAbove())
                return Forbid("Unauthorized access.");

            if (source.Status != "active" || dest.Status != "active") return BadRequest("Account not active.");
            if (source.AvailableBalance < request.Amount) return BadRequest("Insufficient funds.");

            var tx = new Models.Transaction {
                ReferenceNumber = Guid.NewGuid().ToString("N").ToUpper(),
                TransactionType = "transfer",
                Amount = request.Amount,
                SourceAccountId = source.AccountId,
                DestAccountId = dest.AccountId,
                InitiatedBy = currentUserId,
                StateId = (int)TransactionStateEnum.Pending,
                CreatedAt = DateTime.UtcNow
            };
            
            _db.Transactions.Add(tx);
            await _db.SaveChangesAsync();

            source.AvailableBalance -= request.Amount;
            source.Balance -= request.Amount;
            dest.AvailableBalance += request.Amount;
            dest.Balance += request.Amount;

            _db.TransactionEntries.Add(new TransactionEntry { TransactionId = tx.TransactionId, AccountId = source.AccountId, EntryType = "debit",
             Amount = request.Amount, BalanceBefore = source.Balance + request.Amount, BalanceAfter = source.Balance, CreatedAt = DateTime.UtcNow });
            _db.TransactionEntries.Add(new TransactionEntry { TransactionId = tx.TransactionId, AccountId = dest.AccountId, EntryType = "credit",
             Amount = request.Amount, BalanceBefore = dest.Balance - request.Amount, BalanceAfter = dest.Balance, CreatedAt = DateTime.UtcNow });

            tx.StateId = (int)TransactionStateEnum.Completed;
            await CreateAuditLogAsync("Transfer", tx.TransactionId.ToString(), $"Transferred {request.Amount} from {source.AccountId} to {dest.AccountId}");

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(new { tx.ReferenceNumber });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            var correlationId = Guid.NewGuid();
            _logger.LogError(ex, "Transfer failed. ID: {CorrelationId}", correlationId);
            return StatusCode(500, new { message = "An internal error occurred.", correlationId });
        }
    }

    [HttpPost("deposit")]
    [Authorize(Roles = "teller,manager,admin")]
    public async Task<IActionResult> Deposit([FromBody] DepositRequest request)
    {
        if (request.Amount <= 0) return BadRequest("Amount must be positive.");

        using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var account = await _db.Accounts.FindAsync(request.AccountId);
            if (account == null) return NotFound();

            account.Balance += request.Amount;
            account.AvailableBalance += request.Amount;

            var tx = new Models.Transaction {
                ReferenceNumber = Guid.NewGuid().ToString("N").ToUpper(),
                TransactionType = "deposit",
                Amount = request.Amount,
                DestAccountId = account.AccountId,
                InitiatedBy = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value),
                StateId = (int)TransactionStateEnum.Completed,
                CreatedAt = DateTime.UtcNow
            };
            _db.Transactions.Add(tx);
            await _db.SaveChangesAsync();

            _db.TransactionEntries.Add(new TransactionEntry { TransactionId = tx.TransactionId, AccountId = account.AccountId, EntryType = "credit"
            , Amount = request.Amount, BalanceBefore = account.Balance - request.Amount, BalanceAfter = account.Balance, CreatedAt = DateTime.UtcNow });

            await CreateAuditLogAsync("Deposit", tx.TransactionId.ToString(), $"Deposited {request.Amount} to {account.AccountId}");

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(new { tx.ReferenceNumber });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            var correlationId = Guid.NewGuid();
            _logger.LogError(ex, "Deposit failed. ID: {CorrelationId}", correlationId);
            return StatusCode(500, new { message = "An internal error occurred.", correlationId });
        }
    }

    [HttpPost("withdrawal")]
    [Authorize(Roles = "teller,manager,admin")]
    public async Task<IActionResult> Withdrawal([FromBody] WithdrawalRequest request)
    {
        if (request.Amount <= 0) return BadRequest("Amount must be positive.");

        var limitError = await CheckLimitAsync(request.AccountId, "daily_withdrawal", request.Amount);
        if (limitError != null) return BadRequest(limitError);

        using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var account = await _db.Accounts.FindAsync(request.AccountId);
            if (account == null) return NotFound();
            if (account.AvailableBalance < request.Amount) return BadRequest("Insufficient funds.");

            account.Balance -= request.Amount;
            account.AvailableBalance -= request.Amount;

            var tx = new Models.Transaction {
                ReferenceNumber = Guid.NewGuid().ToString("N").ToUpper(),
                TransactionType = "withdrawal",
                Amount = request.Amount,
                SourceAccountId = account.AccountId,
                InitiatedBy = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value),
                StateId = (int)TransactionStateEnum.Completed,
                CreatedAt = DateTime.UtcNow
            };
            _db.Transactions.Add(tx);
            await _db.SaveChangesAsync();

            _db.TransactionEntries.Add(new TransactionEntry { TransactionId = tx.TransactionId, AccountId = account.AccountId, EntryType = "debit",
             Amount = request.Amount, BalanceBefore = account.Balance + request.Amount, BalanceAfter = account.Balance, CreatedAt = DateTime.UtcNow });

            await CreateAuditLogAsync("Withdrawal", tx.TransactionId.ToString(), $"Withdrew {request.Amount} from {account.AccountId}");

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(new { tx.ReferenceNumber });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            var correlationId = Guid.NewGuid();
            _logger.LogError(ex, "Withdrawal failed. ID: {CorrelationId}", correlationId);
            return StatusCode(500, new { message = "An internal error occurred.", correlationId });
        }
    }

    [HttpGet("history/{accountId}")]
    public async Task<IActionResult> History(Guid accountId, [FromQuery] TransactionHistoryRequest request)
    {
        if (!IsTellerOrAbove())
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var account = await _db.Accounts.FindAsync(accountId);
            if (account == null || account.UserId != userId)
                return Forbid("Unauthorized access.");
        }

        var query = _db.Transactions
            .Include(t => t.State)
            .Where(t => t.SourceAccountId == accountId || t.DestAccountId == accountId);

        if (request.StartDate.HasValue) query = query.Where(t => t.CreatedAt >= request.StartDate.Value);
        if (request.EndDate.HasValue) query = query.Where(t => t.CreatedAt <= request.EndDate.Value);
        if (!string.IsNullOrWhiteSpace(request.TransactionType)) query = query.Where(t => t.TransactionType == request.TransactionType);
        if (!string.IsNullOrWhiteSpace(request.State)) query = query.Where(t => t.State.StateName == request.State);

        var total = await query.CountAsync();
        var history = await query.OrderByDescending(t => t.CreatedAt)
                                 .Skip((request.Page - 1) * request.PageSize)
                                 .Take(request.PageSize)
                                 .ToListAsync();

        return Ok(new { total, request.Page, request.PageSize, data = history });
    }
}

using System.Net;
using System.Security.Claims;
using System.Text.Json;
using BankingApi.Data;
using BankingApi.DTOs;
using BankingApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankingApi.Controllers;

[ApiController]
[Route("api/accounts")]
[Authorize]
public class AccountsController : ControllerBase
{
    private static readonly HashSet<string> StaffRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "teller",
        "manager",
        "admin"
    };

    private readonly BankingDbContext _db;

    public AccountsController(BankingDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// List accounts for the authenticated customer, or for another user when caller is teller+.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<AccountListResponse>> ListAccounts([FromQuery] Guid? userId)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized(new { message = "Invalid or missing authentication token." });
        }

        IQueryable<Account> query = _db.Accounts.AsNoTracking();

        if (IsStaff())
        {
            if (userId.HasValue)
            {
                query = query.Where(a => a.UserId == userId.Value);
            }
        }
        else
        {
            if (userId.HasValue && userId.Value != currentUserId)
            {
                return Forbid();
            }

            query = query.Where(a => a.UserId == currentUserId);
        }

        var accounts = await query
            .OrderBy(a => a.OpenedAt)
            .ToListAsync();

        return Ok(new AccountListResponse
        {
            Accounts = accounts.Select(MapToResponse).ToList()
        });
    }

    /// <summary>
    /// Resolve an account by number for transfers (minimal details, any authenticated user).
    /// </summary>
    [HttpGet("lookup/{accountNumber}")]
    public async Task<ActionResult<AccountLookupResponse>> LookupAccount(string accountNumber)
    {
        var normalized = accountNumber.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return BadRequest(new { message = "Account number is required." });
        }

        var account = await _db.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AccountNumber == normalized);

        if (account is null)
        {
            return NotFound(new { message = "Account not found." });
        }

        return Ok(new AccountLookupResponse
        {
            AccountId = account.AccountId,
            AccountNumber = account.AccountNumber,
            AccountType = account.AccountType,
            Currency = account.Currency,
            Status = account.Status
        });
    }

    /// <summary>
    /// Account details (balance, status, type). Owner or teller+.
    /// </summary>
    [HttpGet("{accountId:guid}")]
    public async Task<ActionResult<AccountResponse>> GetAccount(Guid accountId)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized(new { message = "Invalid or missing authentication token." });
        }

        var account = await _db.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AccountId == accountId);

        if (account is null)
        {
            return NotFound(new { message = "Account not found." });
        }

        if (!CanAccessAccount(account, currentUserId))
        {
            return Forbid();
        }

        return Ok(MapToResponse(account));
    }

    /// <summary>
    /// Open a new account for a user. Teller, manager, or admin only.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "TellerOrAbove")]
    public async Task<ActionResult<AccountResponse>> CreateAccount(CreateAccountRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!TryGetCurrentUserId(out var performedBy))
        {
            return Unauthorized(new { message = "Invalid or missing authentication token." });
        }

        var owner = await _db.Users.FindAsync(request.UserId);
        if (owner is null)
        {
            return BadRequest(new { message = "User not found." });
        }

        if (!owner.IsActive)
        {
            return BadRequest(new { message = "Cannot open an account for a disabled user." });
        }

        var accountType = request.AccountType.Trim().ToLowerInvariant();
        var currency = request.Currency.Trim().ToUpperInvariant();

        if (request.InterestRate.HasValue &&
            accountType is not ("savings" or "fixed_deposit" or "loan"))
        {
            return BadRequest(new { message = "Interest rate applies only to savings, fixed_deposit, or loan accounts." });
        }

        var accountNumber = await GenerateUniqueAccountNumberAsync();
        var now = DateTime.UtcNow;

        var account = new Account
        {
            AccountId = Guid.NewGuid(),
            UserId = request.UserId,
            AccountNumber = accountNumber,
            AccountType = accountType,
            Currency = currency,
            Balance = 0m,
            AvailableBalance = 0m,
            InterestRate = request.InterestRate,
            Status = "active",
            OpenedAt = now,
            ClosedAt = null,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Accounts.Add(account);
        LogAccountOpened(account, owner.Email, performedBy, now);
        await _db.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetAccount),
            new { accountId = account.AccountId },
            MapToResponse(account));
    }

    [HttpPatch("{accountId:guid}/freeze")]
    [Authorize(Policy = "ManagerOrAbove")]
    public async Task<ActionResult<AccountResponse>> FreezeAccount(Guid accountId)
    {
        return await UpdateAccountStatusAsync(accountId, "frozen", requireZeroBalance: false);
    }

    [HttpPatch("{accountId:guid}/close")]
    [Authorize(Policy = "ManagerOrAbove")]
    public async Task<ActionResult<AccountResponse>> CloseAccount(Guid accountId)
    {
        return await UpdateAccountStatusAsync(accountId, "closed", requireZeroBalance: true);
    }

    private async Task<ActionResult<AccountResponse>> UpdateAccountStatusAsync(
        Guid accountId,
        string newStatus,
        bool requireZeroBalance)
    {
        if (!TryGetCurrentUserId(out var performedBy))
        {
            return Unauthorized(new { message = "Invalid or missing authentication token." });
        }

        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId);
        if (account is null)
        {
            return NotFound(new { message = "Account not found." });
        }

        if (string.Equals(account.Status, newStatus, StringComparison.OrdinalIgnoreCase))
        {
            return Ok(MapToResponse(account));
        }

        if (requireZeroBalance && account.Balance != 0)
        {
            return BadRequest(new { message = "Account balance must be zero before closing." });
        }

        if (string.Equals(account.Status, "closed", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Closed accounts cannot be modified." });
        }

        var previousStatus = account.Status;
        var now = DateTime.UtcNow;

        account.Status = newStatus;
        account.UpdatedAt = now;
        if (newStatus == "closed")
        {
            account.ClosedAt = now;
        }

        var auditEntry = new AuditLogEntry
        {
            EventType = "account",
            EntityType = "account",
            EntityId = account.AccountId.ToString(),
            Action = "update",
            PerformedBy = performedBy,
            IpAddress = GetClientIpAddress(),
            UserAgent = Request.Headers.UserAgent.ToString(),
            OldValues = JsonSerializer.SerializeToDocument(new { status = previousStatus }),
            NewValues = JsonSerializer.SerializeToDocument(new { status = newStatus, account.AccountNumber }),
            CreatedAt = now
        };

        _db.AuditLogEntries.Add(auditEntry);
        await _db.SaveChangesAsync();

        return Ok(MapToResponse(account));
    }

    private async Task<string> GenerateUniqueAccountNumberAsync()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var suffix = Random.Shared.Next(10000000, 99999999);
            var candidate = $"SB{DateTime.UtcNow:yyMM}{suffix}";

            if (!await _db.Accounts.AnyAsync(a => a.AccountNumber == candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique account number.");
    }

    private void LogAccountOpened(Account account, string ownerEmail, Guid performedBy, DateTime openedAt)
    {
        var auditEntry = new AuditLogEntry
        {
            EventType = "account",
            EntityType = "account",
            EntityId = account.AccountId.ToString(),
            Action = "create",
            PerformedBy = performedBy,
            IpAddress = GetClientIpAddress(),
            UserAgent = Request.Headers.UserAgent.ToString(),
            NewValues = JsonSerializer.SerializeToDocument(new
            {
                account.AccountNumber,
                account.AccountType,
                account.Currency,
                account.UserId,
                ownerEmail,
                openedAt
            }),
            CreatedAt = openedAt
        };

        _db.AuditLogEntries.Add(auditEntry);
    }

    private IPAddress? GetClientIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress;
    }

    private bool CanAccessAccount(Account account, Guid currentUserId)
    {
        return account.UserId == currentUserId || IsStaff();
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = default;

        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(subject, out userId);
    }

    private bool IsStaff()
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        return role is not null && StaffRoles.Contains(role);
    }

    private static AccountResponse MapToResponse(Account account) => new()
    {
        AccountId = account.AccountId,
        UserId = account.UserId,
        AccountNumber = account.AccountNumber,
        AccountType = account.AccountType,
        Currency = account.Currency,
        Balance = account.Balance,
        AvailableBalance = account.AvailableBalance,
        InterestRate = account.InterestRate,
        Status = account.Status,
        OpenedAt = account.OpenedAt,
        ClosedAt = account.ClosedAt
    };
}

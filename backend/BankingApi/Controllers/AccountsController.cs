using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using BankingApi.Data;
using BankingApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankingApi.Controllers
{
    [ApiController]
    [Route("api/accounts")]
    [Authorize] 
    public class AccountsController : ControllerBase
    {
        private readonly BankingDbContext _db;

        private const int DefaultPageSize = 20;
        private const int MaxPageSize = 100;
        private static readonly Guid SystemActorId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        private static readonly string[] AllowedAccountTypes = { "checking", "savings" };
        private static readonly string[] AllowedCurrencies = { "USD", "EUR", "GBP" };

        public AccountsController(BankingDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetAccounts(
            [FromQuery] Guid? userId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = DefaultPageSize,
            CancellationToken cancellationToken = default)
        {
            var currentUserRole = GetCurrentUserRole();

            if (!TryGetCurrentUserId(out var currentUserId) || string.IsNullOrEmpty(currentUserRole))
            {
                return Unauthorized();
            }

            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? DefaultPageSize : pageSize > MaxPageSize ? MaxPageSize : pageSize;

            IQueryable<Account> query = _db.Accounts;

            if (currentUserRole is "teller" or "manager" or "admin")
            {
                if (userId.HasValue)
                    query = query.Where(a => a.UserId == userId.Value);
            }
            else 
            {
                if (userId.HasValue && userId.Value != currentUserId)
                    return Forbid();
                query = query.Where(a => a.UserId == currentUserId);
            }

            var totalItems = await query.CountAsync(cancellationToken);
            var accounts = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => MapToResponse(a))
                .ToListAsync(cancellationToken);

            return Ok(new { totalItems, page, pageSize, data = accounts });
        }

        [HttpGet("{accountId}")]
        public async Task<IActionResult> GetAccountById(Guid accountId, CancellationToken cancellationToken = default)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
                return Unauthorized();

            var account = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId, cancellationToken);
            if (account == null)
                return NotFound(new { message = "Account not found." });

            var currentUserRole = GetCurrentUserRole();
            if (account.UserId != currentUserId &&
                currentUserRole != "teller" && currentUserRole != "manager" && currentUserRole != "admin")
            {
                return Forbid();
            }

            return Ok(MapToResponse(account));
        }

        [HttpPost]
        [Authorize(Roles = "teller,manager,admin")]
        public async Task<IActionResult> CreateAccount([FromBody] CreateAccountRequest request, CancellationToken cancellationToken = default)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            var typeNormalized = request.AccountType.Trim().ToLowerInvariant();
            if (!AllowedAccountTypes.Contains(typeNormalized))
                return BadRequest(new { message = $"Invalid account type. Allowed: {string.Join(", ", AllowedAccountTypes)}" });

            var currencyNormalized = (request.Currency ?? "USD").Trim().ToUpperInvariant();
            if (!AllowedCurrencies.Contains(currencyNormalized))
                return BadRequest(new { message = $"Unsupported currency. Allowed: {string.Join(", ", AllowedCurrencies)}" });

            if (!await _db.Users.AnyAsync(u => u.UserId == request.UserId, cancellationToken))
                return BadRequest(new { message = "Target user does not exist." });

            var alreadyHasType = await _db.Accounts.AnyAsync(a => a.UserId == request.UserId && a.AccountType == typeNormalized && a.Status == "active", cancellationToken);
            if (alreadyHasType)
                return BadRequest(new { message = $"User already holds an active '{typeNormalized}' account." });

            var now = DateTime.UtcNow;
            var accountNumber = await GenerateUniqueAccountNumberAsync(cancellationToken);

            var newAccount = new Account
            {
                AccountId = Guid.NewGuid(),
                UserId = request.UserId,
                AccountNumber = accountNumber,
                AccountType = typeNormalized,
                Currency = currencyNormalized,
                Balance = 0m,
                AvailableBalance = 0m,
                Status = "active",
                OpenedAt = now,
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.Accounts.Add(newAccount);

            var auditEntry = new AuditLogEntry
            {
                EventType = "account_management",
                EntityType = nameof(Account),
                EntityId = newAccount.AccountId.ToString(),
                Action = "create",
                PerformedBy = TryGetCurrentUserId(out var actorId) ? actorId : (Guid?)SystemActorId,
                IpAddress = HttpContext.Connection.RemoteIpAddress,
                UserAgent = Request.Headers.UserAgent.ToString(),
                AdditionalInfo = JsonSerializer.SerializeToDocument(new
                {
                    newAccount.AccountNumber,
                    newAccount.AccountType,
                    ClientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
                }),
                CreatedAt = now
            };
            _db.AuditLogEntries.Add(auditEntry);

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                return StatusCode(StatusCodes.Status409Conflict, new { message = "Account number generation conflict. Please retry your request safely." });
            }

            return StatusCode(StatusCodes.Status201Created, MapToResponse(newAccount));
        }

        [HttpPatch("{accountId}/freeze")]
        [Authorize(Roles = "manager,admin")]
        public async Task<IActionResult> FreezeAccount(Guid accountId, CancellationToken cancellationToken = default)
        {
            var account = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId, cancellationToken);
            if (account == null)
                return NotFound(new { message = "Account not found." });

            if (account.Status == "closed")
                return BadRequest(new { message = "Cannot freeze a closed account." });
            if (account.Status == "frozen")
                return BadRequest(new { message = "Account is already frozen." });

            var oldStatus = account.Status;
            var now = DateTime.UtcNow;

            account.Status = "frozen";
            account.UpdatedAt = now;

            var auditEntry = new AuditLogEntry
            {
                EventType = "account_management",
                EntityType = nameof(Account),
                EntityId = account.AccountId.ToString(),
                Action = "freeze",
                PerformedBy = TryGetCurrentUserId(out var actorId) ? actorId : (Guid?)SystemActorId,
                IpAddress = HttpContext.Connection.RemoteIpAddress,
                UserAgent = Request.Headers.UserAgent.ToString(),
                OldValues = JsonSerializer.SerializeToDocument(new { status = oldStatus }),
                NewValues = JsonSerializer.SerializeToDocument(new { status = "frozen" }),
                CreatedAt = now
            };
            _db.AuditLogEntries.Add(auditEntry);

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { message = "Account frozen successfully.", accountId = account.AccountId });
        }

        [HttpPatch("{accountId}/close")]
        [Authorize(Roles = "manager,admin")]
        public async Task<IActionResult> CloseAccount(Guid accountId, CancellationToken cancellationToken = default)
        {
            var account = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId, cancellationToken);
            if (account == null)
                return NotFound(new { message = "Account not found." });

            if (account.Status == "closed")
                return BadRequest(new { message = "Account is already closed." });
            if (account.Status == "frozen")
                return BadRequest(new { message = "Cannot close a frozen account. Unfreeze it first." });
            if (account.Balance != 0m)
                return BadRequest(new { message = "Cannot close account with remaining balance." });

            var oldStatus = account.Status;
            var now = DateTime.UtcNow;

            account.Status = "closed";
            account.ClosedAt = now;
            account.UpdatedAt = now;

            var auditEntry = new AuditLogEntry
            {
                EventType = "account_management",
                EntityType = nameof(Account),
                EntityId = account.AccountId.ToString(),
                Action = "close",
                PerformedBy = TryGetCurrentUserId(out var actorId) ? actorId : (Guid?)SystemActorId,
                IpAddress = HttpContext.Connection.RemoteIpAddress,
                UserAgent = Request.Headers.UserAgent.ToString(),
                OldValues = JsonSerializer.SerializeToDocument(new { status = oldStatus }),
                NewValues = JsonSerializer.SerializeToDocument(new { status = "closed", closedAt = now }),
                CreatedAt = now
            };
            _db.AuditLogEntries.Add(auditEntry);

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { message = "Account closed successfully.", accountId = account.AccountId });
        }

        #region Internal Helper Routines

        private bool TryGetCurrentUserId(out Guid userId)
        {
            userId = Guid.Empty;
            var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                             User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
            return Guid.TryParse(claimValue, out userId);
        }

        private string GetCurrentUserRole() => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        private static string GenerateSecureAccountNumber()
        {
            var bytes = new byte[8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            ulong val = BitConverter.ToUInt64(bytes, 0) % 900_000_000_000L + 100_000_000_000L;
            return val.ToString();
        }

        private async Task<string> GenerateUniqueAccountNumberAsync(CancellationToken cancellationToken, int maxRetries = 3)
        {
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                var number = GenerateSecureAccountNumber();
                var exists = await _db.Accounts.AnyAsync(a => a.AccountNumber == number, cancellationToken);
                if (!exists)
                    return number;
            }
            throw new InvalidOperationException("Unable to generate a unique account number after multiple attempts.");
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            return ex.InnerException?.Message.Contains("UNIQUE") == true ||
                   ex.InnerException?.Message.Contains("2627") == true ||
                   ex.InnerException?.Message.Contains("23505") == true;
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
            Status = account.Status,
            OpenedAt = account.OpenedAt
        };

        #endregion
    }

    #region Data Transfer Objects (DTOs)

    public class CreateAccountRequest
    {
        [Required]
        public Guid UserId { get; set; }

        [Required]
        [StringLength(20, MinimumLength = 3)]
        public string AccountType { get; set; } = string.Empty;

        [StringLength(3, MinimumLength = 3)]
        public string? Currency { get; set; }
    }

    public class AccountResponse
    {
        public Guid AccountId { get; set; }
        public Guid UserId { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public string Currency { get; set; } = "USD";
        public decimal Balance { get; set; }
        public decimal AvailableBalance { get; set; }
        public string Status { get; set; } = "active";
        public DateTime OpenedAt { get; set; }
    }

    #endregion
}
using System.Data;
using System.Net;
using System.Text.Json;
using BankingApi.Data;
using BankingApi.DTOs;
using BankingApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BankingApi.Services;

public class TransactionService
{
    private static readonly string[] TransferLimitTypes =
    {
        "single_transaction",
        "daily_transfer",
        "monthly_transfer"
    };

    private static readonly string[] WithdrawalLimitTypes =
    {
        "single_transaction",
        "daily_withdrawal",
        "monthly_withdrawal"
    };

    private readonly BankingDbContext _db;

    public TransactionService(BankingDbContext db)
    {
        _db = db;
    }

    public Task<TransactionResponse> ExecuteTransferAsync(
        TransferRequest request,
        Guid initiatedBy,
        bool isStaff,
        IPAddress? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (request.SourceAccountId == request.DestAccountId)
        {
            throw new TransferException("Source and destination accounts must be different.");
        }

        return ExecuteTransferInternalAsync(
            request.SourceAccountId,
            request.DestAccountId,
            request.Amount,
            request.Description,
            initiatedBy,
            isStaff,
            ipAddress,
            userAgent,
            cancellationToken);
    }

    public Task<TransactionResponse> ExecuteDepositAsync(
        DepositRequest request,
        Guid initiatedBy,
        IPAddress? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        return ExecuteDepositInternalAsync(
            request.AccountId,
            request.Amount,
            request.Description,
            initiatedBy,
            ipAddress,
            userAgent,
            cancellationToken);
    }

    public Task<TransactionResponse> ExecuteWithdrawalAsync(
        WithdrawalRequest request,
        Guid initiatedBy,
        IPAddress? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWithdrawalInternalAsync(
            request.AccountId,
            request.Amount,
            request.Description,
            initiatedBy,
            ipAddress,
            userAgent,
            cancellationToken);
    }

    public async Task<TransactionHistoryResponse> GetHistoryAsync(
        Guid accountId,
        Guid userId,
        bool isStaff,
        TransactionHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        var account = await _db.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AccountId == accountId, cancellationToken);

        if (account is null)
        {
            throw new TransferException("Account not found.");
        }

        if (!isStaff && account.UserId != userId)
        {
            throw new TransferException("You do not have access to this account history.");
        }

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var transactionsQuery =
            from t in _db.Transactions.AsNoTracking()
            join s in _db.TransactionStates.AsNoTracking() on t.StateId equals s.StateId
            where t.SourceAccountId == accountId || t.DestAccountId == accountId
            select new { Transaction = t, StateName = s.StateName };

        if (query.StartDate.HasValue)
        {
            transactionsQuery = transactionsQuery.Where(x => x.Transaction.CreatedAt >= query.StartDate.Value);
        }

        if (query.EndDate.HasValue)
        {
            transactionsQuery = transactionsQuery.Where(x => x.Transaction.CreatedAt <= query.EndDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.TransactionType))
        {
            var type = query.TransactionType.Trim().ToLowerInvariant();
            transactionsQuery = transactionsQuery.Where(x => x.Transaction.TransactionType == type);
        }

        if (!string.IsNullOrWhiteSpace(query.State))
        {
            var state = query.State.Trim().ToLowerInvariant();
            transactionsQuery = transactionsQuery.Where(x => x.StateName == state);
        }

        var totalCount = await transactionsQuery.CountAsync(cancellationToken);

        var items = await transactionsQuery
            .OrderByDescending(x => x.Transaction.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new TransactionHistoryResponse
        {
            AccountId = accountId,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Transactions = items.Select(x => MapToResponse(x.Transaction, x.StateName)).ToList()
        };
    }

    private async Task<TransactionResponse> ExecuteTransferInternalAsync(
        Guid sourceAccountId,
        Guid destAccountId,
        decimal amount,
        string? description,
        Guid initiatedBy,
        bool isStaff,
        IPAddress? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        ValidateAmount(amount);

        var pendingStateId = await GetStateIdAsync("pending", cancellationToken);
        var completedStateId = await GetStateIdAsync("completed", cancellationToken);

        await using var dbTransaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var source = await _db.Accounts.FirstAsync(a => a.AccountId == sourceAccountId, cancellationToken);
            var dest = await _db.Accounts.FirstAsync(a => a.AccountId == destAccountId, cancellationToken);

            if (!isStaff && source.UserId != initiatedBy)
            {
                throw new TransferException("You can only transfer from your own accounts.");
            }

            ValidateAccountActive(source, "Source");
            ValidateAccountActive(dest, "Destination");

            if (!string.Equals(source.Currency, dest.Currency, StringComparison.OrdinalIgnoreCase))
            {
                throw new TransferException("Source and destination accounts must use the same currency.");
            }

            if (source.AvailableBalance < amount)
            {
                throw new TransferException("Insufficient available balance.");
            }

            await EnsureLimitsAllowAsync(source.AccountId, amount, TransferLimitTypes, source.Currency, cancellationToken);

            var now = DateTime.UtcNow;
            var transaction = CreateTransaction(
                "transfer",
                amount,
                source.Currency,
                description,
                pendingStateId,
                source.AccountId,
                dest.AccountId,
                initiatedBy,
                ipAddress,
                now);

            _db.Transactions.Add(transaction);

            var sourceBalanceBefore = source.Balance;
            var destBalanceBefore = dest.Balance;

            ApplyDebit(source, amount, now);
            ApplyCredit(dest, amount, now);

            AddTransferEntries(transaction.TransactionId, source, dest, amount, sourceBalanceBefore, destBalanceBefore, now);
            await ApplyLimitUsageAsync(source.AccountId, amount, TransferLimitTypes, cancellationToken);

            FinalizeTransaction(transaction, completedStateId, now);
            LogTransactionAudit(transaction, "create", initiatedBy, ipAddress, userAgent, now, new
            {
                transaction.ReferenceNumber,
                transaction.Amount,
                sourceAccountNumber = source.AccountNumber,
                destAccountNumber = dest.AccountNumber
            });

            await _db.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);

            return MapToResponse(transaction, "completed");
        }
        catch (TransferException)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw;
        }
        catch (Exception)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw new TransferException("Transfer could not be completed. Please try again.");
        }
    }

    private async Task<TransactionResponse> ExecuteDepositInternalAsync(
        Guid accountId,
        decimal amount,
        string? description,
        Guid initiatedBy,
        IPAddress? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        ValidateAmount(amount);

        var pendingStateId = await GetStateIdAsync("pending", cancellationToken);
        var completedStateId = await GetStateIdAsync("completed", cancellationToken);

        await using var dbTransaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var account = await _db.Accounts.FirstAsync(a => a.AccountId == accountId, cancellationToken);
            ValidateAccountActive(account, "Target");

            var now = DateTime.UtcNow;
            var transaction = CreateTransaction(
                "deposit",
                amount,
                account.Currency,
                description,
                pendingStateId,
                null,
                account.AccountId,
                initiatedBy,
                ipAddress,
                now);

            _db.Transactions.Add(transaction);

            var balanceBefore = account.Balance;
            ApplyCredit(account, amount, now);

            _db.TransactionEntries.Add(new TransactionEntry
            {
                TransactionId = transaction.TransactionId,
                AccountId = account.AccountId,
                EntryType = "credit",
                Amount = amount,
                BalanceBefore = balanceBefore,
                BalanceAfter = account.Balance,
                CreatedAt = now
            });

            FinalizeTransaction(transaction, completedStateId, now);
            LogTransactionAudit(transaction, "create", initiatedBy, ipAddress, userAgent, now, new
            {
                transaction.ReferenceNumber,
                transaction.Amount,
                account.AccountNumber
            });

            await _db.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);

            return MapToResponse(transaction, "completed");
        }
        catch (TransferException)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw;
        }
        catch (Exception)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw new TransferException("Deposit could not be completed. Please try again.");
        }
    }

    private async Task<TransactionResponse> ExecuteWithdrawalInternalAsync(
        Guid accountId,
        decimal amount,
        string? description,
        Guid initiatedBy,
        IPAddress? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        ValidateAmount(amount);

        var pendingStateId = await GetStateIdAsync("pending", cancellationToken);
        var completedStateId = await GetStateIdAsync("completed", cancellationToken);

        await using var dbTransaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var account = await _db.Accounts.FirstAsync(a => a.AccountId == accountId, cancellationToken);
            ValidateAccountActive(account, "Source");

            if (account.AvailableBalance < amount)
            {
                throw new TransferException("Insufficient available balance.");
            }

            await EnsureLimitsAllowAsync(account.AccountId, amount, WithdrawalLimitTypes, account.Currency, cancellationToken);

            var now = DateTime.UtcNow;
            var transaction = CreateTransaction(
                "withdrawal",
                amount,
                account.Currency,
                description,
                pendingStateId,
                account.AccountId,
                null,
                initiatedBy,
                ipAddress,
                now);

            _db.Transactions.Add(transaction);

            var balanceBefore = account.Balance;
            ApplyDebit(account, amount, now);

            _db.TransactionEntries.Add(new TransactionEntry
            {
                TransactionId = transaction.TransactionId,
                AccountId = account.AccountId,
                EntryType = "debit",
                Amount = amount,
                BalanceBefore = balanceBefore,
                BalanceAfter = account.Balance,
                CreatedAt = now
            });

            await ApplyLimitUsageAsync(account.AccountId, amount, WithdrawalLimitTypes, cancellationToken);

            FinalizeTransaction(transaction, completedStateId, now);
            LogTransactionAudit(transaction, "create", initiatedBy, ipAddress, userAgent, now, new
            {
                transaction.ReferenceNumber,
                transaction.Amount,
                account.AccountNumber
            });

            await _db.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);

            return MapToResponse(transaction, "completed");
        }
        catch (TransferException)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw;
        }
        catch (Exception)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw new TransferException("Withdrawal could not be completed. Please try again.");
        }
    }

    private static void ValidateAmount(decimal amount)
    {
        if (amount <= 0)
        {
            throw new TransferException("Amount must be greater than zero.");
        }
    }

    private static void ValidateAccountActive(Account account, string label)
    {
        if (!string.Equals(account.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            throw new TransferException($"{label} account is not active.");
        }
    }

    private static void ApplyDebit(Account account, decimal amount, DateTime now)
    {
        account.Balance -= amount;
        account.AvailableBalance -= amount;
        account.UpdatedAt = now;
    }

    private static void ApplyCredit(Account account, decimal amount, DateTime now)
    {
        account.Balance += amount;
        account.AvailableBalance += amount;
        account.UpdatedAt = now;
    }

    private void AddTransferEntries(
        Guid transactionId,
        Account source,
        Account dest,
        decimal amount,
        decimal sourceBalanceBefore,
        decimal destBalanceBefore,
        DateTime now)
    {
        _db.TransactionEntries.Add(new TransactionEntry
        {
            TransactionId = transactionId,
            AccountId = source.AccountId,
            EntryType = "debit",
            Amount = amount,
            BalanceBefore = sourceBalanceBefore,
            BalanceAfter = source.Balance,
            CreatedAt = now
        });

        _db.TransactionEntries.Add(new TransactionEntry
        {
            TransactionId = transactionId,
            AccountId = dest.AccountId,
            EntryType = "credit",
            Amount = amount,
            BalanceBefore = destBalanceBefore,
            BalanceAfter = dest.Balance,
            CreatedAt = now
        });
    }

    private static Transaction CreateTransaction(
        string type,
        decimal amount,
        string currency,
        string? description,
        int pendingStateId,
        Guid? sourceAccountId,
        Guid? destAccountId,
        Guid initiatedBy,
        IPAddress? ipAddress,
        DateTime now)
    {
        var prefix = type switch
        {
            "deposit" => "DEP",
            "withdrawal" => "WDR",
            _ => "TRF"
        };

        return new Transaction
        {
            TransactionId = Guid.NewGuid(),
            ReferenceNumber = $"{prefix}{now:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}",
            TransactionType = type,
            Amount = amount,
            Currency = currency,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            StateId = pendingStateId,
            SourceAccountId = sourceAccountId,
            DestAccountId = destAccountId,
            InitiatedBy = initiatedBy,
            IpAddress = ipAddress,
            Channel = "web",
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static void FinalizeTransaction(Transaction transaction, int completedStateId, DateTime now)
    {
        transaction.StateId = completedStateId;
        transaction.ProcessedAt = now;
        transaction.UpdatedAt = now;
    }

    private async Task EnsureLimitsAllowAsync(
        Guid accountId,
        decimal amount,
        string[] limitTypes,
        string currency,
        CancellationToken cancellationToken)
    {
        var limits = await _db.AccountLimits
            .Where(l => l.AccountId == accountId && l.IsActive && limitTypes.Contains(l.LimitType))
            .ToListAsync(cancellationToken);

        if (limits.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var limit in limits)
        {
            ResetLimitUsageIfNeeded(limit, now);

            if (limit.CurrentUsage + amount > limit.MaxAmount)
            {
                throw new TransferException(
                    $"Transaction exceeds {limit.LimitType.Replace('_', ' ')} limit of {limit.MaxAmount:N2} {currency}.");
            }
        }
    }

    private async Task ApplyLimitUsageAsync(
        Guid accountId,
        decimal amount,
        string[] limitTypes,
        CancellationToken cancellationToken)
    {
        var limits = await _db.AccountLimits
            .Where(l => l.AccountId == accountId && l.IsActive && limitTypes.Contains(l.LimitType))
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var limit in limits)
        {
            ResetLimitUsageIfNeeded(limit, now);
            limit.CurrentUsage += amount;
            limit.UpdatedAt = now;
        }
    }

    private static void ResetLimitUsageIfNeeded(AccountLimit limit, DateTime now)
    {
        if (limit.UsageResetAt > now)
        {
            return;
        }

        limit.CurrentUsage = 0;
        limit.UsageResetAt = limit.LimitType switch
        {
            "daily_transfer" or "daily_withdrawal" => now.Date.AddDays(1),
            "monthly_transfer" or "monthly_withdrawal" => new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1),
            _ => now.AddDays(1)
        };
        limit.UpdatedAt = now;
    }

    private async Task<int> GetStateIdAsync(string stateName, CancellationToken cancellationToken)
    {
        var stateId = await _db.TransactionStates
            .AsNoTracking()
            .Where(s => s.StateName == stateName)
            .Select(s => s.StateId)
            .FirstOrDefaultAsync(cancellationToken);

        if (stateId == 0)
        {
            throw new InvalidOperationException($"Transaction state '{stateName}' is not configured.");
        }

        return stateId;
    }

    private void LogTransactionAudit(
        Transaction transaction,
        string action,
        Guid performedBy,
        IPAddress? ipAddress,
        string? userAgent,
        DateTime at,
        object details)
    {
        _db.AuditLogEntries.Add(new AuditLogEntry
        {
            EventType = "transaction",
            EntityType = "transaction",
            EntityId = transaction.TransactionId.ToString(),
            Action = action,
            PerformedBy = performedBy,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            NewValues = JsonSerializer.SerializeToDocument(details),
            CreatedAt = at
        });
    }

    private static TransactionResponse MapToResponse(Transaction transaction, string stateName) => new()
    {
        TransactionId = transaction.TransactionId,
        ReferenceNumber = transaction.ReferenceNumber,
        TransactionType = transaction.TransactionType,
        Amount = transaction.Amount,
        Currency = transaction.Currency,
        State = stateName,
        SourceAccountId = transaction.SourceAccountId,
        DestAccountId = transaction.DestAccountId,
        Description = transaction.Description,
        CreatedAt = transaction.CreatedAt,
        ProcessedAt = transaction.ProcessedAt
    };
}

public class TransferException : Exception
{
    public TransferException(string message) : base(message)
    {
    }
}

// Transaction Service — core business logic
//
// What to implement here:
// - ExecuteTransfer(sourceAccountId, destAccountId, amount, initiatedBy)
//     1. Validate both accounts exist and are active
//     2. Check account_limits for the source account
//     3. Begin DB transaction
//     4. Create Transaction row (state = pending)
//     5. Create debit TransactionEntry (source) + credit TransactionEntry (dest)
//     6. Update source balance and available_balance (subtract)
//     7. Update dest balance and available_balance (add)
//     8. Update current_usage on relevant account_limits
//     9. Set Transaction state to completed
//    10. Commit DB transaction (rollback on any failure → state = failed)
//    11. Write audit_log entry
//
// - ExecuteDeposit(accountId, amount, initiatedBy)
// - ExecuteWithdrawal(accountId, amount, initiatedBy)
// - CheckLimits(accountId, amount, limitType) → bool

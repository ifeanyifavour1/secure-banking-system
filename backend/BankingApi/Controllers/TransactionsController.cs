// POST /api/transactions/transfer
//   - [Authorize] Transfer funds between two accounts
//   - Validate: source account belongs to user (or Teller+), account is active
//   - Check account_limits (daily_transfer, single_transaction, monthly_transfer)
//   - Create Transaction row (state = pending)
//   - Create debit TransactionEntry on source + credit TransactionEntry on dest
//   - Update balances atomically (use DB transaction)
//   - Set Transaction state to completed (or failed on error)
//   - Log to audit_log
//
// POST /api/transactions/deposit
//   - [TellerOrAbove] Deposit funds into an account
//   - Create Transaction (source = NULL, dest = target account)
//   - Create credit TransactionEntry, update balance
//
// POST /api/transactions/withdrawal
//   - [TellerOrAbove] Withdraw funds from an account
//   - Check account_limits (daily_withdrawal, single_transaction)
//   - Create Transaction (source = target account, dest = NULL)
//   - Create debit TransactionEntry, update balance
//
// GET /api/transactions/history/{accountId}
//   - [Authorize] Paginated transaction history for an account
//   - Filter by date range, transaction_type, state
//   - Owner sees own; Teller+ can view any

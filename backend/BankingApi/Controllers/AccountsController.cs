// GET /api/accounts
//   - [Authorize] Return all accounts for the authenticated user
//   - Teller/Manager/Admin can query accounts for any user
//
// GET /api/accounts/{accountId}
//   - [Authorize] Return account details (balance, available_balance, status, type)
//   - Owner sees own account; Teller+ can view any account
//
// POST /api/accounts
//   - [TellerOrAbove] Open a new account for a user
//   - Set account_type, currency, generate unique account_number
//   - Initial balance = 0, status = 'active'
//   - Log to audit_log
//
// PATCH /api/accounts/{accountId}/freeze
//   - [ManagerOrAbove] Set account status to 'frozen'
//   - Prevents all transactions on the account
//   - Log to audit_log with old_values/new_values
//
// PATCH /api/accounts/{accountId}/close
//   - [ManagerOrAbove] Set status to 'closed', populate closed_at
//   - Only if balance == 0

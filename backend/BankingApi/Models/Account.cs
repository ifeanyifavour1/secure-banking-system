// Account entity — maps to the 'accounts' table
//
// Fields:
//   account_id (UUID PK), user_id (FK → users), account_number (unique),
//   account_type (checking|savings|fixed_deposit|loan), currency, balance,
//   available_balance, interest_rate, status (active|frozen|closed|dormant),
//   opened_at, closed_at, created_at, updated_at

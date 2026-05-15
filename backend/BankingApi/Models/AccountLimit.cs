// AccountLimit entity — maps to the 'account_limits' table
//
// Fields:
//   limit_id (INT PK), account_id (FK → accounts),
//   limit_type (daily_withdrawal|daily_transfer|single_transaction|monthly_withdrawal|monthly_transfer),
//   max_amount, current_usage, usage_reset_at, is_active,
//   created_at, updated_at
//
// Composite unique constraint: (account_id, limit_type)

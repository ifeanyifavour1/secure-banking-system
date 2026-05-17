// Transaction entity — maps to the 'transactions' table
//
// Fields:
//   transaction_id (UUID PK), reference_number (unique), transaction_type,
//   amount, currency, description, state_id (FK → transaction_states),
//   source_account_id (FK → accounts), dest_account_id (FK → accounts),
//   initiated_by (FK → users), ip_address (INET), channel,
//   scheduled_at, processed_at, created_at, updated_at
//
// ---
// TransactionState entity — maps to 'transaction_states' table
//   state_id (INT PK), state_name (unique), description
//
// ---
// TransactionEntry entity — maps to 'transaction_entries' table (double-entry bookkeeping)
//   entry_id (BIGINT PK), transaction_id (FK), account_id (FK),
//   entry_type (debit|credit), amount, balance_before, balance_after, created_at

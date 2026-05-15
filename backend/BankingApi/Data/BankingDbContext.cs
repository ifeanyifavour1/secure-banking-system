// EF Core DbContext for PostgreSQL (Neon)
//
// What to implement here:
// - DbSet for each table: users, accounts, transactions, transaction_entries,
//   transaction_states, account_limits, audit_log
// - OnModelCreating: map C# models to snake_case PostgreSQL table/column names
// - Configure unique indexes (email, national_id, account_number, reference_number)
// - Configure foreign key relationships
// - Configure composite unique constraint on account_limits (account_id, limit_type)

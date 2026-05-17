// User entity — maps to the 'users' table
//
// Fields:
//   user_id (UUID PK), first_name, last_name, email (unique), phone_number,
//   password_hash (BYTEA), password_salt (BYTEA), date_of_birth, national_id (unique),
//   address_line1, address_line2, city, country, postal_code,
//   role (customer|teller|manager|admin), mfa_enabled, mfa_secret,
//   is_active, is_locked, failed_login_count, last_login_at,
//   created_at, updated_at

// POST /api/auth/login
//   - Accept email + password
//   - Verify credentials against users table (bcrypt/Argon2 hash)
//   - Check is_locked and failed_login_count for lockout policy
//   - Verify MFA (TOTP) if mfa_enabled is true
//   - On success: issue JWT access token + refresh token, reset failed_login_count
//   - On failure: increment failed_login_count, lock account after threshold
//   - Log to audit_log (login or failed_login)
//
// POST /api/auth/register
//   - Accept user details (name, email, password, national_id, date_of_birth)
//   - Validate input (unique email, unique national_id, password strength)
//   - Hash password with random salt using bcrypt/Argon2
//   - Create user record with role = 'customer'
//   - Log to audit_log (create action)
//
// POST /api/auth/refresh
//   - Validate refresh token
//   - Issue new JWT access token + refresh token pair

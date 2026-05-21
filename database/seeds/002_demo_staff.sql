-- Demo staff users for local testing (run against Neon after 001_initial_schema.sql)
--
-- 1) Register via API or Flask UI (valid phone +71234567890, etc.):
--      admin@demo.bank   / AdminPass1!
--      teller@demo.bank  / TellerPass1!
--      customer@demo.bank (optional, for account seed below)
--
-- 2) Promote roles (or use POST /api/internal/staff/role as admin with JWT + X-Admin-Secret):
UPDATE users
SET role = 'admin',
    updated_at = NOW()
WHERE email = 'admin@demo.bank';

UPDATE users
SET role = 'teller',
    updated_at = NOW()
WHERE email = 'teller@demo.bank';

-- 3) Optional: open a checking account for a customer (register customer@demo.bank first)
INSERT INTO accounts (
    user_id,
    account_number,
    account_type,
    currency,
    balance,
    available_balance,
    status,
    opened_at,
    created_at,
    updated_at
)
SELECT
    u.user_id,
    'CHK' || UPPER(SUBSTRING(REPLACE(gen_random_uuid()::text, '-', '') FROM 1 FOR 12)),
    'checking',
    'USD',
    0.00,
    0.00,
    'active',
    NOW(),
    NOW(),
    NOW()
FROM users u
WHERE u.email = 'customer@demo.bank'
  AND NOT EXISTS (
      SELECT 1 FROM accounts a WHERE a.user_id = u.user_id
  );

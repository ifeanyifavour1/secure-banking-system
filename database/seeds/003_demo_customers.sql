-- Two demo customers for transfer / login testing (run in Neon SQL editor)
-- Requires: 001_initial_schema.sql applied
-- Passwords: Customer1! and Customer2! (upper, lower, digit, special)

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- Customer 1: Alice
INSERT INTO users (
    user_id,
    first_name,
    last_name,
    email,
    phone_number,
    password_hash,
    password_salt,
    date_of_birth,
    national_id,
    address_line1,
    city,
    country,
    postal_code,
    role,
    mfa_enabled,
    is_active,
    is_locked,
    failed_login_count,
    created_at,
    updated_at
)
VALUES (
    '11111111-1111-1111-1111-111111111101',
    'Alice',
    'Demo',
    'alice@demo.bank',
    '+71234567801',
    convert_to(crypt('Customer1!', gen_salt('bf', 12)), 'UTF8'),
    '\x'::bytea,
    '1995-03-15',
    'DEMO-NID-ALICE-001',
    '1 Demo Street',
    'Tomsk',
    'RU',
    '634000',
    'customer',
    FALSE,
    TRUE,
    FALSE,
    0,
    NOW(),
    NOW()
)
ON CONFLICT (email) DO NOTHING;

-- Customer 2: Bob
INSERT INTO users (
    user_id,
    first_name,
    last_name,
    email,
    phone_number,
    password_hash,
    password_salt,
    date_of_birth,
    national_id,
    address_line1,
    city,
    country,
    postal_code,
    role,
    mfa_enabled,
    is_active,
    is_locked,
    failed_login_count,
    created_at,
    updated_at
)
VALUES (
    '22222222-2222-2222-2222-222222222202',
    'Bob',
    'Demo',
    'bob@demo.bank',
    '+71234567802',
    convert_to(crypt('Customer2!', gen_salt('bf', 12)), 'UTF8'),
    '\x'::bytea,
    '1996-07-20',
    'DEMO-NID-BOB-002',
    '2 Demo Street',
    'Tomsk',
    'RU',
    '634000',
    'customer',
    FALSE,
    TRUE,
    FALSE,
    0,
    NOW(),
    NOW()
)
ON CONFLICT (email) DO NOTHING;

-- Alice: checking + savings (for transfers between own accounts)
INSERT INTO accounts (
    account_id, user_id, account_number, account_type, currency,
    balance, available_balance, status, opened_at, created_at, updated_at
)
VALUES (
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    '11111111-1111-1111-1111-111111111101',
    'SB2603ALICECHK01',
    'checking',
    'USD',
    1000.00,
    1000.00,
    'active',
    NOW(), NOW(), NOW()
)
ON CONFLICT (account_number) DO NOTHING;

INSERT INTO accounts (
    account_id, user_id, account_number, account_type, currency,
    balance, available_balance, status, opened_at, created_at, updated_at
)
VALUES (
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa02',
    '11111111-1111-1111-1111-111111111101',
    'SB2603ALICESAV01',
    'savings',
    'USD',
    500.00,
    500.00,
    'active',
    NOW(), NOW(), NOW()
)
ON CONFLICT (account_number) DO NOTHING;

-- Bob: one checking account
INSERT INTO accounts (
    account_id, user_id, account_number, account_type, currency,
    balance, available_balance, status, opened_at, created_at, updated_at
)
VALUES (
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb01',
    '22222222-2222-2222-2222-222222222202',
    'SB2603BOBCHK0001',
    'checking',
    'USD',
    750.00,
    750.00,
    'active',
    NOW(), NOW(), NOW()
)
ON CONFLICT (account_number) DO NOTHING;

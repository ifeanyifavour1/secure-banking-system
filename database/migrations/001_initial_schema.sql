-- ============================================================
-- Banking System Database Schema (PostgreSQL)
-- Secure Online Banking Platform
-- ============================================================

-- ============================================================
-- 1. USERS
-- ============================================================
CREATE TABLE users (
    user_id             UUID                PRIMARY KEY DEFAULT gen_random_uuid(),
    first_name          VARCHAR(100)        NOT NULL,
    last_name           VARCHAR(100)        NOT NULL,
    email               VARCHAR(255)        NOT NULL UNIQUE,
    phone_number        VARCHAR(20),
    password_hash       BYTEA               NOT NULL,
    password_salt       BYTEA               NOT NULL,
    date_of_birth       DATE                NOT NULL,
    national_id         VARCHAR(50)         NOT NULL UNIQUE,
    address_line1       VARCHAR(255),
    address_line2       VARCHAR(255),
    city                VARCHAR(100),
    country             VARCHAR(100),
    postal_code         VARCHAR(20),
    role                VARCHAR(20)         NOT NULL DEFAULT 'customer'
                        CHECK (role IN ('customer', 'teller', 'manager', 'admin')),
    mfa_enabled         BOOLEAN             NOT NULL DEFAULT FALSE,
    mfa_secret          BYTEA,
    is_active           BOOLEAN             NOT NULL DEFAULT TRUE,
    is_locked           BOOLEAN             NOT NULL DEFAULT FALSE,
    failed_login_count  INT                 NOT NULL DEFAULT 0,
    last_login_at       TIMESTAMPTZ,
    created_at          TIMESTAMPTZ         NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ         NOT NULL DEFAULT NOW()
);

-- ============================================================
-- 2. ACCOUNTS
-- ============================================================
CREATE TABLE accounts (
    account_id          UUID                PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id             UUID                NOT NULL
                        REFERENCES users(user_id),
    account_number      VARCHAR(20)         NOT NULL UNIQUE,
    account_type        VARCHAR(20)         NOT NULL
                        CHECK (account_type IN ('checking', 'savings', 'fixed_deposit', 'loan')),
    currency            VARCHAR(3)          NOT NULL DEFAULT 'USD',
    balance             NUMERIC(18,2)       NOT NULL DEFAULT 0.00,
    available_balance   NUMERIC(18,2)       NOT NULL DEFAULT 0.00,
    interest_rate       NUMERIC(5,4),
    status              VARCHAR(20)         NOT NULL DEFAULT 'active'
                        CHECK (status IN ('active', 'frozen', 'closed', 'dormant')),
    opened_at           TIMESTAMPTZ         NOT NULL DEFAULT NOW(),
    closed_at           TIMESTAMPTZ,
    created_at          TIMESTAMPTZ         NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ         NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_accounts_user_id ON accounts(user_id);

-- ============================================================
-- 3. TRANSACTION STATES
-- ============================================================
CREATE TABLE transaction_states (
    state_id            INT                 GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    state_name          VARCHAR(30)         NOT NULL UNIQUE,
    description         VARCHAR(255)
);

INSERT INTO transaction_states (state_name, description) VALUES
    ('pending',     'Transaction initiated, awaiting processing'),
    ('authorized',  'Transaction authorized but not yet settled'),
    ('completed',   'Transaction fully processed and settled'),
    ('failed',      'Transaction failed during processing'),
    ('reversed',    'Transaction reversed after completion'),
    ('cancelled',   'Transaction cancelled before settlement'),
    ('on_hold',     'Transaction flagged and held for review');

-- ============================================================
-- 4. TRANSACTIONS
-- ============================================================
CREATE TABLE transactions (
    transaction_id      UUID                PRIMARY KEY DEFAULT gen_random_uuid(),
    reference_number    VARCHAR(50)         NOT NULL UNIQUE,
    transaction_type    VARCHAR(30)         NOT NULL
                        CHECK (transaction_type IN (
                            'deposit', 'withdrawal', 'transfer',
                            'payment', 'fee', 'interest', 'refund'
                        )),
    amount              NUMERIC(18,2)       NOT NULL CHECK (amount > 0),
    currency            VARCHAR(3)          NOT NULL DEFAULT 'USD',
    description         VARCHAR(500),
    state_id            INT                 NOT NULL
                        REFERENCES transaction_states(state_id),
    source_account_id   UUID
                        REFERENCES accounts(account_id),
    dest_account_id     UUID
                        REFERENCES accounts(account_id),
    initiated_by        UUID                NOT NULL
                        REFERENCES users(user_id),
    ip_address          INET,
    channel             VARCHAR(20)
                        CHECK (channel IN ('web', 'mobile', 'atm', 'branch', 'api')),
    scheduled_at        TIMESTAMPTZ,
    processed_at        TIMESTAMPTZ,
    created_at          TIMESTAMPTZ         NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ         NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_transactions_source_account ON transactions(source_account_id);
CREATE INDEX idx_transactions_dest_account   ON transactions(dest_account_id);
CREATE INDEX idx_transactions_state_id       ON transactions(state_id);
CREATE INDEX idx_transactions_created_at     ON transactions(created_at);

-- ============================================================
-- 5. TRANSACTION ENTRIES (Double-Entry Bookkeeping)
-- ============================================================
CREATE TABLE transaction_entries (
    entry_id            BIGINT              GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    transaction_id      UUID                NOT NULL
                        REFERENCES transactions(transaction_id),
    account_id          UUID                NOT NULL
                        REFERENCES accounts(account_id),
    entry_type          VARCHAR(6)          NOT NULL
                        CHECK (entry_type IN ('debit', 'credit')),
    amount              NUMERIC(18,2)       NOT NULL CHECK (amount > 0),
    balance_before      NUMERIC(18,2)       NOT NULL,
    balance_after       NUMERIC(18,2)       NOT NULL,
    created_at          TIMESTAMPTZ         NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_transaction_entries_tx_id      ON transaction_entries(transaction_id);
CREATE INDEX idx_transaction_entries_account_id  ON transaction_entries(account_id);

-- ============================================================
-- 6. ACCOUNT LIMITS
-- ============================================================
CREATE TABLE account_limits (
    limit_id            INT                 GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    account_id          UUID                NOT NULL
                        REFERENCES accounts(account_id),
    limit_type          VARCHAR(30)         NOT NULL
                        CHECK (limit_type IN (
                            'daily_withdrawal', 'daily_transfer',
                            'single_transaction', 'monthly_withdrawal',
                            'monthly_transfer'
                        )),
    max_amount          NUMERIC(18,2)       NOT NULL CHECK (max_amount > 0),
    current_usage       NUMERIC(18,2)       NOT NULL DEFAULT 0.00,
    usage_reset_at      TIMESTAMPTZ         NOT NULL,
    is_active           BOOLEAN             NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMPTZ         NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ         NOT NULL DEFAULT NOW(),

    CONSTRAINT uq_account_limits_account_type UNIQUE (account_id, limit_type)
);

CREATE INDEX idx_account_limits_account_id ON account_limits(account_id);

-- ============================================================
-- 7. AUDIT LOG
-- ============================================================
CREATE TABLE audit_log (
    log_id              BIGINT              GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    event_type          VARCHAR(50)         NOT NULL,
    entity_type         VARCHAR(50)         NOT NULL,
    entity_id           VARCHAR(100)        NOT NULL,
    action              VARCHAR(20)         NOT NULL
                        CHECK (action IN ('create', 'read', 'update', 'delete', 'login', 'logout', 'failed_login')),
    performed_by        UUID
                        REFERENCES users(user_id),
    old_values          JSONB,
    new_values          JSONB,
    ip_address          INET,
    user_agent          VARCHAR(500),
    additional_info     JSONB,
    created_at          TIMESTAMPTZ         NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_audit_log_entity       ON audit_log(entity_type, entity_id);
CREATE INDEX idx_audit_log_performed_by ON audit_log(performed_by);
CREATE INDEX idx_audit_log_created_at   ON audit_log(created_at);
CREATE INDEX idx_audit_log_action       ON audit_log(action);

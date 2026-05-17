-- Seed data: transaction lifecycle states
-- Run after 001_initial_schema.sql (states are also included there via INSERT)

INSERT INTO transaction_states (state_name, description) VALUES
    ('pending',     'Transaction initiated, awaiting processing'),
    ('authorized',  'Transaction authorized but not yet settled'),
    ('completed',   'Transaction fully processed and settled'),
    ('failed',      'Transaction failed during processing'),
    ('reversed',    'Transaction reversed after completion'),
    ('cancelled',   'Transaction cancelled before settlement'),
    ('on_hold',     'Transaction flagged and held for review')
ON CONFLICT (state_name) DO NOTHING;

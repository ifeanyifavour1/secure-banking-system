# Routes package — Flask blueprints for the frontend
#
# This package contains:
# - auth.py:         Login, register, logout routes (calls backend /api/auth/*)
# - dashboard.py:    Account list and account detail views (calls backend /api/accounts/*)
# - transactions.py: Transfer form and transaction history (calls backend /api/transactions/*)
#
# Each module defines a Blueprint that is registered in app/__init__.py

# Dashboard blueprint — /dashboard/*
#
# GET /dashboard/
#   - Fetch user's accounts from backend GET /api/accounts
#   - Pass JWT from session in Authorization header
#   - Render accounts list (account_number, type, balance, status)
#
# GET /dashboard/account/<account_id>
#   - Fetch account details from backend GET /api/accounts/{accountId}
#   - Fetch recent transactions from GET /api/transactions/history/{accountId}
#   - Render account detail page with transaction list

# Transactions blueprint — /transactions/*
#
# GET/POST /transactions/transfer
#   - Render transfer form (destination account, amount, description)
#   - On POST: send to backend POST /api/transactions/transfer
#   - Show success/error message
#
# GET /transactions/history/<account_id>
#   - Fetch paginated history from backend GET /api/transactions/history/{accountId}
#   - Render table: date, type, amount, state, reference_number

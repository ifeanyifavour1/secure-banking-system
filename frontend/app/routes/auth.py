# Auth blueprint — /auth/*
#
# GET/POST /auth/login
#   - Render login form
#   - On POST: send credentials to backend POST /api/auth/login
#   - Store JWT in session on success
#   - Redirect to dashboard
#
# GET/POST /auth/register
#   - Render registration form
#   - On POST: send user data to backend POST /api/auth/register
#   - Redirect to login on success
#
# GET /auth/logout
#   - Clear session (remove JWT)
#   - Redirect to login page

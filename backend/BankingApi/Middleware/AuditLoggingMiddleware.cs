// Audit Logging Middleware
//
// What to implement here:
// - Intercept every request
// - Extract: IP address, user agent, user_id from JWT claims
// - After response completes, write to audit_log table
// - Capture: endpoint hit, HTTP method, response status code
// - For sensitive endpoints (login, transfer), log additional detail

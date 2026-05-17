// JWT Token Service
//
// What to implement here:
// - GenerateAccessToken(user) → string
//     Include claims: user_id, email, role
//     Short expiry (e.g. 30 minutes)
// - GenerateRefreshToken() → string
//     Cryptographically random, longer expiry (e.g. 7 days)
// - ValidateRefreshToken(token) → claims or null

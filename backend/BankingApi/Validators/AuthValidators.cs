// Authentication input validators
//
// What to implement here:
//
// LoginRequestValidator (FluentValidation or DataAnnotations):
//   - Email: required, valid email format
//   - Password: required, not empty
//
// RegisterRequestValidator:
//   - FirstName: required, max 50 chars, letters only
//   - LastName: required, max 50 chars, letters only
//   - Email: required, valid email, must be unique (check DB)
//   - Password: required, min 8 chars, at least 1 uppercase, 1 lowercase, 1 digit, 1 special char
//   - NationalId: required, must be unique (check DB)
//   - DateOfBirth: required, must be at least 18 years ago
//
// Sanitize all string inputs to prevent XSS and SQL injection

// Authentication & Registration DTOs
//
// What to implement here:
//
// LoginRequest:
//   - Email (string, required)
//   - Password (string, required)
//   - TotpCode (string, optional — only if MFA enabled)
//
// RegisterRequest:
//   - FirstName, LastName (string, required)
//   - Email (string, required, valid email format)
//   - Password (string, required, min 8 chars)
//   - NationalId (string, required, unique)
//   - DateOfBirth (DateTime, required)
//   - PhoneNumber (string, optional)
//
// AuthResponse:
//   - AccessToken (string — JWT)
//   - RefreshToken (string)
//   - ExpiresAt (DateTime)
//
// RefreshRequest:
//   - RefreshToken (string, required)

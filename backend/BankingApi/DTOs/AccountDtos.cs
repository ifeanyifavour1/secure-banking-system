// Account DTOs
//
// What to implement here:
//
// CreateAccountRequest:
//   - UserId (Guid, required)
//   - AccountType (string: checking | savings | fixed_deposit | loan)
//   - Currency (string, default "USD")
//
// AccountResponse:
//   - AccountId (Guid)
//   - AccountNumber (string)
//   - AccountType (string)
//   - Currency (string)
//   - Balance (decimal)
//   - AvailableBalance (decimal)
//   - Status (string: active | frozen | closed | dormant)
//   - OpenedAt (DateTime)
//
// AccountListResponse:
//   - Accounts (List<AccountResponse>)

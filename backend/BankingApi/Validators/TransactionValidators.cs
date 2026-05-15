// Transaction input validators
//
// What to implement here:
//
// TransferRequestValidator:
//   - SourceAccountId: required, valid GUID
//   - DestAccountId: required, valid GUID, must differ from source
//   - Amount: required, > 0, max 2 decimal places
//   - Description: optional, max 500 chars, sanitized
//
// DepositRequestValidator:
//   - AccountId: required, valid GUID
//   - Amount: required, > 0
//
// WithdrawalRequestValidator:
//   - AccountId: required, valid GUID
//   - Amount: required, > 0
//
// AccountIdValidator (reusable):
//   - Must be a valid GUID
//   - Account must exist in DB
//   - Account status must be 'active' (not frozen/closed)

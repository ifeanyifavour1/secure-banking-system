// Transaction DTOs
//
// What to implement here:
//
// TransferRequest:
//   - SourceAccountId (Guid, required)
//   - DestAccountId (Guid, required)
//   - Amount (decimal, required, > 0)
//   - Currency (string)
//   - Description (string, optional, max 500 chars)
//
// DepositRequest:
//   - AccountId (Guid, required)
//   - Amount (decimal, required, > 0)
//   - Description (string, optional)
//
// WithdrawalRequest:
//   - AccountId (Guid, required)
//   - Amount (decimal, required, > 0)
//   - Description (string, optional)
//
// TransactionResponse:
//   - TransactionId (Guid)
//   - ReferenceNumber (string)
//   - TransactionType (string)
//   - Amount (decimal)
//   - Currency (string)
//   - State (string)
//   - Description (string)
//   - CreatedAt (DateTime)
//
// TransactionHistoryRequest:
//   - AccountId (Guid, required)
//   - StartDate, EndDate (DateTime, optional — filter range)
//   - TransactionType (string, optional — filter)
//   - State (string, optional — filter)
//   - Page (int, default 1)
//   - PageSize (int, default 20)

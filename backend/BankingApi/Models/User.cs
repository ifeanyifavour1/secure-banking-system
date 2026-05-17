namespace BankingApi.Models
{
    public class User
    {
        public Guid UserId { get; set; }

        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }

        public byte[] PasswordHash { get; set; } = Array.Empty<byte>();
        public byte[] PasswordSalt { get; set; } = Array.Empty<byte>();
        
        public DateOnly DateOfBirth { get; set; }
        public string NationalId { get; set; } = string.Empty;

        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? PostalCode { get; set; }

        public string Role { get; set; } = "customer";

        public bool MfaEnabled { get; set; }
        public byte[]? MfaSecret { get; set; }

        public bool IsActive { get; set; }
        public bool IsLocked { get; set; }
        public int FailedLoginCount { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
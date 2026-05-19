using System.ComponentModel.DataAnnotations;

namespace BankingApi.DTOs{


    public class LoginRequest{
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;

        public string? TotpCode { get; set; }
    }

    public class RegisterRequest{
        [Required]
        [StringLength(50, MinimumLength = 2)]
        [RegularExpression(@"^[a-zA-Z]+$")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(50, MinimumLength = 2)]
        [RegularExpression(@"^[a-zA-Z]+$")]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [UniqueEmail]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 8)]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [UniqueNationalId]
        public string NationalId { get; set; } = string.Empty;

        [Required]
        [DateOfBirth]
        public DateOnly DateOfBirth { get; set; }

        [Required]
        [RegularExpression(@"^\+7\d{10}$", ErrorMessage = "Phone number must start with +7 and contain 10 digits after it.")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^[a-zA-Z0-9\s]+$")]
        public string AddressLine1 { get; set; } = string.Empty;

        [AddressLine2]
        public string? AddressLine2 { get; set; }

        [Required]
        [City]
        public string City { get; set; } = string.Empty;
        
        [Required]
        [Country]
        public string Country { get; set; } = string.Empty;

        [Required]
        [PostalCode]
        public string PostalCode { get; set; } = string.Empty;
        
        
    }

    public class RefreshTokenRequest
    {
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class AuthResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}

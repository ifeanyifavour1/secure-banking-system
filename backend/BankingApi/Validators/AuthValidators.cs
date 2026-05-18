using System.ComponentModel.DataAnnotations;
using BankingApi.Data;

namespace BankingApi.DTOs
{
    public class UniqueEmailAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is not string email || string.IsNullOrWhiteSpace(email))
            {
                return ValidationResult.Success;
            }

            var db = validationContext.GetService(typeof(BankingDbContext)) as BankingDbContext;
            if (db is null)
            {
                return ValidationResult.Success;
            }

            var normalizedEmail = email.Trim().ToLowerInvariant();
            var emailExists = db.Users.Any(user => user.Email.ToLower() == normalizedEmail);

            return emailExists
                ? new ValidationResult("Email is already registered.")
                : ValidationResult.Success;
        }
    }

    public class UniqueNationalIdAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is not string nationalId || string.IsNullOrWhiteSpace(nationalId))
            {
                return ValidationResult.Success;
            }

            var db = validationContext.GetService(typeof(BankingDbContext)) as BankingDbContext;
            if (db is null)
            {
                return ValidationResult.Success;
            }

            var normalizedNationalId = nationalId.Trim();
            var nationalIdExists = db.Users.Any(user => user.NationalId == normalizedNationalId);

            return nationalIdExists
                ? new ValidationResult("National ID is already registered.")
                : ValidationResult.Success;
        }
    }

    public class DateOfBirthAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is not DateOnly dateOfBirth || dateOfBirth == default)
            {
                return new ValidationResult("Date of birth is required.");
            }

            var minimumBirthDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-18));
            if (dateOfBirth > minimumBirthDate)
            {
                return new ValidationResult("Customer must be at least 18 years old.");
            }

            return ValidationResult.Success;
        }
    }

    public class AddressLine2Attribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is null)
            {
                return ValidationResult.Success;
            }

            if (value is not string addressLine2)
            {
                return new ValidationResult("Address line 2 is invalid.");
            }

            if (string.IsNullOrWhiteSpace(addressLine2))
            {
                return ValidationResult.Success;
            }

            return addressLine2.Length <= 100 && addressLine2.All(character => char.IsLetterOrDigit(character) || char.IsWhiteSpace(character))
                ? ValidationResult.Success
                : new ValidationResult("Address line 2 can only contain letters, numbers, and spaces.");
        }
    }

    public class CityAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is not string city || string.IsNullOrWhiteSpace(city))
            {
                return new ValidationResult("City is required.");
            }

            return city.Length <= 50 && city.All(character => char.IsLetter(character) || char.IsWhiteSpace(character) || character == '-')
                ? ValidationResult.Success
                : new ValidationResult("City can only contain letters, spaces, and hyphens.");
        }
    }

    public class CountryAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is not string country || string.IsNullOrWhiteSpace(country))
            {
                return new ValidationResult("Country is required.");
            }

            return country.Length <= 56 && country.All(character => char.IsLetter(character) || char.IsWhiteSpace(character) || character == '-')
                ? ValidationResult.Success
                : new ValidationResult("Country can only contain letters, spaces, and hyphens.");
        }
    }

    public class PostalCodeAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is not string postalCode || string.IsNullOrWhiteSpace(postalCode))
            {
                return new ValidationResult("Postal code is required.");
            }

            return postalCode.Length <= 12 && postalCode.All(character => char.IsLetterOrDigit(character) || character == '-' || char.IsWhiteSpace(character))
                ? ValidationResult.Success
                : new ValidationResult("Postal code can only contain letters, numbers, spaces, and hyphens.");
        }
    }
}

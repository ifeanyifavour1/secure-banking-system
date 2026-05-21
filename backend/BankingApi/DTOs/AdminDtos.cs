using System.ComponentModel.DataAnnotations;

namespace BankingApi.DTOs;

public class SetUserRoleRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [RegularExpression(
        @"^(customer|teller|manager|admin)$",
        ErrorMessage = "Role must be customer, teller, manager, or admin.")]
    public string Role { get; set; } = string.Empty;
}

public class SetUserRoleResponse
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PreviousRole { get; set; } = string.Empty;
    public string NewRole { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

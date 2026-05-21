namespace BankingApi.Auth;

public class AdminSettings
{
    /// <summary>
    /// When set, callers must send this value in the X-Admin-Secret header (in addition to admin JWT).
    /// </summary>
    public string? RoleAssignmentSecret { get; set; }
}

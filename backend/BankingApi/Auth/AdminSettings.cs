namespace BankingApi.Auth;

public class AdminSettings
{
    /// <summary>
    /// Required for role assignment. Callers must send this value in X-Admin-Secret (in addition to admin JWT).
    /// </summary>
    public string? RoleAssignmentSecret { get; set; }
}

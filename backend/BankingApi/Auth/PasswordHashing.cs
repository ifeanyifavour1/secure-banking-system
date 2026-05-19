using System.Text;

namespace BankingApi.Auth;

public static class PasswordHashing
{
    public static byte[] HashPassword(string password, int workFactor)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor);
        return Encoding.UTF8.GetBytes(hash);
    }

    public static bool Verify(string password, byte[] passwordHash)
    {
        if (passwordHash.Length == 0)
        {
            return false;
        }

        var hash = Encoding.UTF8.GetString(passwordHash);
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }

    public static bool NeedsRehash(byte[] passwordHash, int workFactor)
    {
        if (passwordHash.Length == 0)
        {
            return true;
        }

        var hash = Encoding.UTF8.GetString(passwordHash);
        return BCrypt.Net.BCrypt.PasswordNeedsRehash(hash, workFactor);
    }
}

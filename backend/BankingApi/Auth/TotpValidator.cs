using System.Text;
using OtpNet;

namespace BankingApi.Auth;

public static class TotpValidator
{
    public static bool Validate(byte[]? mfaSecret, string? totpCode)
    {
        if (mfaSecret is null or { Length: 0 } || string.IsNullOrWhiteSpace(totpCode))
        {
            return false;
        }

        byte[] secretBytes;
        try
        {
            var secretString = Encoding.UTF8.GetString(mfaSecret);
            secretBytes = Base32Encoding.ToBytes(secretString);
        }
        catch (FormatException)
        {
            return false;
        }

        var totp = new Totp(secretBytes);
        return totp.VerifyTotp(
            totpCode.Trim(),
            out _,
            new VerificationWindow(previous: 1, future: 1));
    }
}

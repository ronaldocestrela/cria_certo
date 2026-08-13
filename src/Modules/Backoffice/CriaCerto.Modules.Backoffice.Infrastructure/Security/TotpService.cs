using System.Security.Cryptography;
using System.Text;
using CriaCerto.Modules.Backoffice.Application.Security;

namespace CriaCerto.Modules.Backoffice.Infrastructure.Security;

public class TotpService : ITotpService
{
    private static readonly char[] Base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".ToCharArray();

    public string GenerateSecretKey()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(20);
        var result = new StringBuilder(32);
        foreach (byte b in bytes)
        {
            result.Append(Base32Chars[b % 32]);
        }
        return result.ToString();
    }

    public string GenerateQrCodeUri(string email, string secretKey, string issuer = "CriaCerto Backoffice")
    {
        string encodedIssuer = Uri.EscapeDataString(issuer);
        string encodedEmail = Uri.EscapeDataString(email);
        return $"otpauth://totp/{encodedIssuer}:{encodedEmail}?secret={secretKey}&issuer={encodedIssuer}&digits=6&period=30";
    }

    public bool VerifyCode(string secretKey, string code, int timeWindowSeconds = 30)
    {
        if (string.IsNullOrWhiteSpace(secretKey) || string.IsNullOrWhiteSpace(code) || code.Length != 6)
            return false;

        byte[] secretBytes;
        try
        {
            secretBytes = Base32Decode(secretKey);
        }
        catch
        {
            return false;
        }

        long currentStep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;

        // Check current step as well as -1 and +1 window for clock drift
        for (int stepOffset = -1; stepOffset <= 1; stepOffset++)
        {
            string expectedCode = GenerateTotpForStep(secretBytes, currentStep + stepOffset);
            if (CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(code.Trim()),
                Encoding.UTF8.GetBytes(expectedCode)))
            {
                return true;
            }
        }

        return false;
    }

    public List<string> GenerateRecoveryCodes(int count = 8)
    {
        var codes = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
            byte[] bytes = RandomNumberGenerator.GetBytes(5);
            string hex = Convert.ToHexString(bytes).Substring(0, 8);
            codes.Add($"{hex.Substring(0, 4)}-{hex.Substring(4, 4)}");
        }
        return codes;
    }

    private static string GenerateTotpForStep(byte[] secret, long step)
    {
        byte[] stepBytes = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(step));
        using var hmac = new HMACSHA1(secret);
        byte[] hash = hmac.ComputeHash(stepBytes);

        int offset = hash[^1] & 0x0F;
        int binaryCode = ((hash[offset] & 0x7F) << 24)
                       | ((hash[offset + 1] & 0xFF) << 16)
                       | ((hash[offset + 2] & 0xFF) << 8)
                       | (hash[offset + 3] & 0xFF);

        int otp = binaryCode % 1_000_000;
        return otp.ToString("D6");
    }

    private static byte[] Base32Decode(string base32)
    {
        base32 = base32.TrimEnd('=').ToUpperInvariant();
        var bytes = new List<byte>();
        int bitBuffer = 0;
        int bitBufferLength = 0;

        foreach (char c in base32)
        {
            int charValue = Array.IndexOf(Base32Chars, c);
            if (charValue < 0) continue;

            bitBuffer = (bitBuffer << 5) | charValue;
            bitBufferLength += 5;

            if (bitBufferLength >= 8)
            {
                bitBufferLength -= 8;
                bytes.Add((byte)((bitBuffer >> bitBufferLength) & 0xFF));
            }
        }

        return bytes.ToArray();
    }
}

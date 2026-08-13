namespace CriaCerto.Modules.Backoffice.Application.Security;

public interface ITotpService
{
    string GenerateSecretKey();
    string GenerateQrCodeUri(string email, string secretKey, string issuer = "CriaCerto Backoffice");
    bool VerifyCode(string secretKey, string code, int timeWindowSeconds = 30);
    List<string> GenerateRecoveryCodes(int count = 8);
}

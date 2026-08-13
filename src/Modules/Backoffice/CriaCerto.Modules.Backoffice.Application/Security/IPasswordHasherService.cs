namespace CriaCerto.Modules.Backoffice.Application.Security;

public interface IPasswordHasherService
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string hashedPassword);
}

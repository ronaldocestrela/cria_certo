using CriaCerto.Modules.Backoffice.Application.Domain.Entities;

namespace CriaCerto.Modules.Backoffice.Application.Security;

public interface IBackofficeTokenService
{
    string GenerateAccessToken(AdminUser user, string tokenId, TimeSpan duration);

    string GenerateRefreshToken();

    string? GetTokenId(string accessToken);
}

using CriaCerto.Modules.Backoffice.Application.Domain.Entities;

namespace CriaCerto.Modules.Backoffice.Application.Security;

public interface IBackofficeTokenService
{
    string GenerateAccessToken(AdminUser user, string tokenId, TimeSpan duration);

    string GenerateRefreshToken();

    string? GetTokenId(string accessToken);

    string GenerateImpersonationToken(
        Guid adminUserId,
        string adminUserEmail,
        Guid tenantId,
        string tenantName,
        Guid? targetUserId,
        string? targetUserEmail,
        Guid sessionId,
        string supportTicket,
        TimeSpan duration);
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CriaCerto.Modules.Backoffice.Infrastructure.Security;

public sealed class BackofficeTokenService : IBackofficeTokenService
{
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;

    public BackofficeTokenService(IConfiguration configuration)
    {
        _secretKey = configuration["Jwt:SecretKey"]
            ?? configuration["JwtSettings:Secret"]
            ?? configuration["JWT_SECRET"]
            ?? "CriaCertoSuperSecretKeyThatIsAtLeast32BytesLong!";
        _issuer = configuration["Jwt:Issuer"] ?? configuration["JwtSettings:Issuer"] ?? "CriaCerto";
        _audience = configuration["Jwt:Audience"] ?? configuration["JwtSettings:Audience"] ?? "CriaCertoClient";
    }

    public string GenerateAccessToken(AdminUser user, string tokenId, TimeSpan duration)
    {
        var claims = BuildClaims(user, tokenId);
        return CreateSignedToken(claims, duration);
    }

    public string GenerateRefreshToken() =>
        Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

    public string GenerateImpersonationToken(
        Guid adminUserId,
        string adminUserEmail,
        Guid tenantId,
        string tenantName,
        Guid? targetUserId,
        string? targetUserEmail,
        Guid sessionId,
        string supportTicket,
        TimeSpan duration)
    {
        var subject = targetUserId.HasValue && targetUserId.Value != Guid.Empty
            ? targetUserId.Value.ToString()
            : adminUserId.ToString();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Sub, subject),
            new(JwtRegisteredClaimNames.Email, targetUserEmail ?? adminUserEmail),
            new("FullName", $"[Suporte] {adminUserEmail}"),
            new("TenantId", tenantId.ToString()),
            new("TenantName", tenantName),
            new(ClaimTypes.Role, "Admin"),
            new("Role", "Admin"),
            new("is_impersonation", "true"),
            new("impersonated_by_admin_id", adminUserId.ToString()),
            new("impersonated_by_admin_email", adminUserEmail),
            new("impersonation_session_id", sessionId.ToString()),
            new("impersonation_ticket", supportTicket)
        };

        return CreateSignedToken(claims, duration);
    }

    public string? GetTokenId(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        try
        {
            var token = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
            return token.Id;
        }
        catch
        {
            return null;
        }
    }

    private List<Claim> BuildClaims(AdminUser user, string tokenId)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, tokenId),
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.Name),
            new("is_backoffice_admin", "true")
        };

        var roleNames = user.Roles.Select(r => r.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var roleName in roleNames)
        {
            claims.Add(new Claim(ClaimTypes.Role, roleName));
            claims.Add(new Claim("role", roleName));
        }

        if (roleNames.Any(r => r.Equals(BackofficeRoles.PlatformOwner, StringComparison.OrdinalIgnoreCase)))
        {
            claims.Add(new Claim("is_platform_owner", "true"));
        }

        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var roleName in roleNames)
        {
            foreach (var permission in BackofficeRoles.GetDefaultPermissionsForRole(roleName))
            {
                permissions.Add(permission);
            }
        }

        foreach (var permission in permissions)
        {
            claims.Add(new Claim("Permission", permission));
        }

        return claims;
    }

    private string CreateSignedToken(IEnumerable<Claim> claims, TimeSpan duration)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(duration),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

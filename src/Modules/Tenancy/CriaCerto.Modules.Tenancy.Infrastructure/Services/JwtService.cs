using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CriaCerto.Modules.Tenancy.Infrastructure.Services;

public sealed class JwtService : IJwtService
{
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;

    public JwtService(IConfiguration configuration)
    {
        _secretKey = configuration["Jwt:SecretKey"] 
            ?? configuration["JwtSettings:Secret"] 
            ?? configuration["JWT_SECRET"] 
            ?? "CriaCertoSuperSecretKeyThatIsAtLeast32BytesLong!";
        _issuer = configuration["Jwt:Issuer"] ?? configuration["JwtSettings:Issuer"] ?? "CriaCerto";
        _audience = configuration["Jwt:Audience"] ?? configuration["JwtSettings:Audience"] ?? "CriaCertoClient";
    }

    public string GenerateToken(User user, Tenant tenant, UserRole role = UserRole.Admin)
    {
        var headTier = PlanCapacityLimits.IsValidHeadTierCode(tenant.HeadTierCode)
            ? PlanCapacityLimits.NormalizeHeadTierCode(tenant.HeadTierCode)
            : PlanCapacityLimits.Tier500;

        var headCapacity = tenant.Capacity > 0
            ? tenant.Capacity
            : PlanCapacityLimits.GetMaxHeadsForTier(headTier);

        var subscribedModules = tenant.GetSubscribedModuleKeys();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("FullName", user.FullName),
            new("TenantId", tenant.Id.ToString()),
            new("TenantName", tenant.Name),
            new("SubscribedPlan", tenant.SubscribedPlan),
            new("head_tier", headTier),
            new("head_capacity", headCapacity.ToString(CultureInfo.InvariantCulture)),
            new("subscribed_modules", string.Join(",", subscribedModules)),
            new(ClaimTypes.Role, role.ToString()),
            new("Role", role.ToString())
        };

        foreach (var moduleKey in subscribedModules)
        {
            claims.Add(new Claim("subscribed_module", moduleKey));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GeneratePartnerToken(User user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim("FullName", user.FullName),
            new Claim(ClaimTypes.Role, "CommercialPartner"),
            new Claim("Role", "CommercialPartner"),
            new Claim("IsPartner", "true")
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

using CriaCerto.Modules.Tenancy.Application.Domain;

namespace CriaCerto.Modules.Tenancy.Application.Abstractions;

public interface IJwtService
{
    string GenerateToken(User user, Tenant tenant, UserRole role = UserRole.Admin);
    string GeneratePartnerToken(User user);
}

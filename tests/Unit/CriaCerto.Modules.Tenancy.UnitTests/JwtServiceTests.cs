using System.IdentityModel.Tokens.Jwt;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class JwtServiceTests
{
    private static JwtService CreateJwtService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "CriaCertoUnitTestsSuperSecretKeyAtLeast32Bytes!",
                ["Jwt:Issuer"] = "CriaCertoTest",
                ["Jwt:Audience"] = "CriaCertoTestClient"
            })
            .Build();

        return new JwtService(configuration);
    }

    [Fact]
    public void GenerateToken_WhenTenantHasModularSubscription_ShouldIncludeHeadTierCapacityAndSubscribedModulesClaims()
    {
        // Arrange
        var jwtService = CreateJwtService();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "produtor@fazenda.com.br",
            FullName = "João Pecuarista"
        };

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Santa Rita",
            SubscribedPlan = "Pro"
        };
        tenant.SetHeadTier(PlanCapacityLimits.Tier1500).IsSuccess.Should().BeTrue();
        tenant.SetSubscribedModules(["Cows", "Breeding", "Sanitary"]).IsSuccess.Should().BeTrue();

        // Act
        var rawToken = jwtService.GenerateToken(user, tenant, UserRole.Admin);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(rawToken);

        jwt.Claims.FirstOrDefault(c => c.Type == "TenantId")?.Value.Should().Be(tenant.Id.ToString());
        jwt.Claims.FirstOrDefault(c => c.Type == "head_tier")?.Value.Should().Be("TIER_1500");
        jwt.Claims.FirstOrDefault(c => c.Type == "head_capacity")?.Value.Should().Be("1500");

        var moduleClaims = jwt.Claims
            .Where(c => c.Type == "subscribed_module")
            .Select(c => c.Value)
            .ToList();
        moduleClaims.Should().BeEquivalentTo(["Cows", "Breeding", "Sanitary"]);

        var compactModulesClaim = jwt.Claims.FirstOrDefault(c => c.Type == "subscribed_modules")?.Value;
        compactModulesClaim.Should().NotBeNullOrWhiteSpace();
        compactModulesClaim!.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Should().BeEquivalentTo(["Cows", "Breeding", "Sanitary"]);
    }

    [Fact]
    public void GenerateToken_WhenTenantHasNoExplicitSubscribedModules_ShouldAlwaysEmitCoreModuleCowsAndDefaultTier()
    {
        // Arrange
        var jwtService = CreateJwtService();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "novo@fazenda.com.br",
            FullName = "Novo Produtor"
        };

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Inicial",
            HeadTierCode = "INVALID_TIER",
            Capacity = 0
        };

        // Act
        var rawToken = jwtService.GenerateToken(user, tenant, UserRole.Veterinario);

        // Assert
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(rawToken);

        jwt.Claims.FirstOrDefault(c => c.Type == "head_tier")?.Value.Should().Be("TIER_500");
        jwt.Claims.FirstOrDefault(c => c.Type == "head_capacity")?.Value.Should().Be("500");
        jwt.Claims.Where(c => c.Type == "subscribed_module").Select(c => c.Value)
            .Should().ContainSingle().Which.Should().Be("Cows");
        jwt.Claims.FirstOrDefault(c => c.Type == "subscribed_modules")?.Value.Should().Be("Cows");
    }
}


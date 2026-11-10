using System.IdentityModel.Tokens.Jwt;
using CriaCerto.Modules.Backoffice.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Security;

public class ImpersonationSecurityTests
{
    private readonly BackofficeTokenService _tokenService;

    public ImpersonationSecurityTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "CriaCertoSuperSecretKeyThatIsAtLeast32BytesLong!",
                ["Jwt:Issuer"] = "CriaCerto",
                ["Jwt:Audience"] = "CriaCertoClient"
            })
            .Build();

        _tokenService = new BackofficeTokenService(config);
    }

    [Fact]
    public void GenerateImpersonationToken_ShouldIncludeAllRequiredClaims()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var adminEmail = "support@criacerto.com.br";
        var tenantId = Guid.NewGuid();
        var tenantName = "Fazenda Triunfo";
        var targetUserId = Guid.NewGuid();
        var targetEmail = "gestor@triunfo.com.br";
        var sessionId = Guid.NewGuid();
        var ticket = "SUP-5544";
        var duration = TimeSpan.FromMinutes(15);

        // Act
        var tokenString = _tokenService.GenerateImpersonationToken(
            adminUserId, adminEmail, tenantId, tenantName, targetUserId, targetEmail, sessionId, ticket, duration);

        // Assert
        tokenString.Should().NotBeNullOrWhiteSpace();

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);

        token.Issuer.Should().Be("CriaCerto");
        token.Audiences.Should().Contain("CriaCertoClient");

        var claims = token.Claims.ToLookup(c => c.Type, c => c.Value);

        claims.Contains(JwtRegisteredClaimNames.Sub).Should().BeTrue();
        claims[JwtRegisteredClaimNames.Sub].First().Should().Be(targetUserId.ToString());

        claims.Contains(JwtRegisteredClaimNames.Email).Should().BeTrue();
        claims[JwtRegisteredClaimNames.Email].First().Should().Be(targetEmail);

        claims.Contains("TenantId").Should().BeTrue();
        claims["TenantId"].First().Should().Be(tenantId.ToString());

        claims.Contains("TenantName").Should().BeTrue();
        claims["TenantName"].First().Should().Be(tenantName);

        claims.Contains("is_impersonation").Should().BeTrue();
        claims["is_impersonation"].First().Should().Be("true");

        claims.Contains("impersonated_by_admin_id").Should().BeTrue();
        claims["impersonated_by_admin_id"].First().Should().Be(adminUserId.ToString());

        claims.Contains("impersonated_by_admin_email").Should().BeTrue();
        claims["impersonated_by_admin_email"].First().Should().Be(adminEmail);

        claims.Contains("impersonation_session_id").Should().BeTrue();
        claims["impersonation_session_id"].First().Should().Be(sessionId.ToString());

        claims.Contains("impersonation_ticket").Should().BeTrue();
        claims["impersonation_ticket"].First().Should().Be(ticket);

        token.ValidTo.Should().BeCloseTo(DateTime.UtcNow.Add(duration), TimeSpan.FromSeconds(5));
    }
}

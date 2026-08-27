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

        var claims = token.Claims.ToDictionary(c => c.Type, c => c.Value);

        claims.Should().ContainKey(JwtRegisteredClaimNames.Sub);
        claims[JwtRegisteredClaimNames.Sub].Should().Be(targetUserId.ToString());

        claims.Should().ContainKey(JwtRegisteredClaimNames.Email);
        claims[JwtRegisteredClaimNames.Email].Should().Be(targetEmail);

        claims.Should().ContainKey("TenantId");
        claims["TenantId"].Should().Be(tenantId.ToString());

        claims.Should().ContainKey("TenantName");
        claims["TenantName"].Should().Be(tenantName);

        claims.Should().ContainKey("is_impersonation");
        claims["is_impersonation"].Should().Be("true");

        claims.Should().ContainKey("impersonated_by_admin_id");
        claims["impersonated_by_admin_id"].Should().Be(adminUserId.ToString());

        claims.Should().ContainKey("impersonated_by_admin_email");
        claims["impersonated_by_admin_email"].Should().Be(adminEmail);

        claims.Should().ContainKey("impersonation_session_id");
        claims["impersonation_session_id"].Should().Be(sessionId.ToString());

        claims.Should().ContainKey("impersonation_ticket");
        claims["impersonation_ticket"].Should().Be(ticket);

        token.ValidTo.Should().BeCloseTo(DateTime.UtcNow.Add(duration), TimeSpan.FromSeconds(5));
    }
}

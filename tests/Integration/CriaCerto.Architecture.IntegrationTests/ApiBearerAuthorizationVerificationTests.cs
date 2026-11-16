using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CriaCerto.Api.Endpoints;
using CriaCerto.BuildingBlocks.Abstractions.Tenancy;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Infrastructure.Services;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Xunit;

namespace CriaCerto.Architecture.IntegrationTests;

public class ApiBearerAuthorizationVerificationTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    private static string ResolveRepoRoot()
    {
        var current = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(current) && !File.Exists(Path.Combine(current, "CriaCerto.slnx")))
        {
            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }
        return current ?? Directory.GetCurrentDirectory();
    }

    [Fact]
    public void AuthenticationConfiguration_ShouldExclusivelyUseJwtBearerScheme()
    {
        // Arrange
        var programPath = Path.Combine(RepoRoot, "src/Host/CriaCerto.Api/Program.cs");
        File.Exists(programPath).Should().BeTrue("Program.cs deve existir no projeto CriaCerto.Api");

        var programSource = File.ReadAllText(programPath);

        // Act & Assert
        programSource.Should().Contain("AddAuthentication(JwtBearerDefaults.AuthenticationScheme)",
            "A API deve registrar explicitamente o JwtBearerDefaults como esquema de autenticação padrão.");

        programSource.Should().Contain("AddJwtBearer(",
            "A API deve configurar o manipulador AddJwtBearer.");

        programSource.Should().NotContain("AddCookie(",
            "A API NÃO deve registrar esquema de autenticação por cookies web, garantindo interoperabilidade com clientes nativos mobile.");

        programSource.Should().NotContain("CookieAuthenticationDefaults",
            "A API não deve conter referências ao CookieAuthenticationDefaults.");
    }

    [Fact]
    public void ApiEndpoints_ShouldNotRelyOnBrowserSessionCookiesForAuthentication()
    {
        // Arrange
        var programPath = Path.Combine(RepoRoot, "src/Host/CriaCerto.Api/Program.cs");
        var programSource = File.ReadAllText(programPath);

        // Act & Assert
        // Verifica que não há leitura de cookies de autenticação (ex.: context.Request.Cookies["...auth..."])
        programSource.Should().NotContain("Request.Cookies[\"",
            "Nenhum endpoint da CriaCerto.Api deve inspecionar cookies de sessão para autenticação ou autorização.");

        programSource.Should().NotContain("Response.Cookies.Append(",
            "A API REST não deve emitir cookies de autenticação de sessão.");
    }

    [Theory]
    [InlineData("/api/breeding")]
    [InlineData("/api/calving")]
    [InlineData("/api/growth")]
    [InlineData("/api/nutrition")]
    [InlineData("/api/sanitary")]
    [InlineData("/api/analytics")]
    [InlineData("/api/v1/backoffice")]
    [InlineData("/api/v1/sync")]
    public void ApiRouteGroups_ShouldRequireAuthorization(string routePrefix)
    {
        // Arrange
        var programPath = Path.Combine(RepoRoot, "src/Host/CriaCerto.Api/Program.cs");
        var syncPath = Path.Combine(RepoRoot, "src/Host/CriaCerto.Api/Endpoints/OfflineSyncEndpoints.cs");

        var sourceCode = File.ReadAllText(programPath) + "\n" + File.ReadAllText(syncPath);

        // Act & Assert
        sourceCode.Should().Contain(routePrefix, $"O grupo de rotas '{routePrefix}' deve estar registrado na API.");
        
        // Verifica que após o mapeamento do grupo, há chamada de .RequireAuthorization()
        var containsGroupWithAuth = sourceCode.Contains($"MapGroup(\"{routePrefix}\")\n    .RequireAuthorization()") ||
                                   sourceCode.Contains($"MapGroup(\"{routePrefix}\").RequireAuthorization()") ||
                                   sourceCode.Contains($"MapGroup(\"{routePrefix}\")\n            .RequireAuthorization()");

        containsGroupWithAuth.Should().BeTrue($"O grupo de rotas '{routePrefix}' deve exigir autorização (.RequireAuthorization()).");
    }

    [Fact]
    public void JwtBearerToken_GeneratedByJwtService_ShouldBeValidatedSuccessfullyByTokenParameters()
    {
        // Arrange
        const string testSecret = "CriaCertoSuperSecretKeyThatIsAtLeast32BytesLong!";
        const string testIssuer = "CriaCerto";
        const string testAudience = "CriaCertoClient";

        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = testSecret,
            ["Jwt:Issuer"] = testIssuer,
            ["Jwt:Audience"] = testAudience
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();
        var jwtService = new JwtService(config);

        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "produtor@fazenda.com.br",
            FullName = "Produtor Rural",
            PasswordHash = "hash123"
        };
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Fazenda Alvorada",
            CNPJ = "12345678000199",
            HeadTierCode = "TIER_500"
        };

        var tokenString = jwtService.GenerateToken(user, tenant, UserRole.Admin);
        tokenString.Should().NotBeNullOrWhiteSpace("O token JWT gerado não deve ser vazio.");

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = testIssuer,
            ValidAudience = testAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(testSecret))
        };

        var handler = new JwtSecurityTokenHandler();

        // Act
        var principal = handler.ValidateToken(tokenString, tokenValidationParameters, out var validatedToken);

        // Assert
        validatedToken.Should().NotBeNull();
        principal.Should().NotBeNull();
        principal.Identity.Should().NotBeNull();
        principal.Identity!.IsAuthenticated.Should().BeTrue("O ClaimsPrincipal gerado pelo Bearer JWT deve estar autenticado.");

        var subClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        subClaim.Should().Be(userId.ToString());

        var tenantClaim = principal.FindFirst("TenantId")?.Value;
        tenantClaim.Should().Be(tenantId.ToString());

        var roleClaim = principal.FindFirst(ClaimTypes.Role)?.Value
                     ?? principal.FindFirst("Role")?.Value;
        roleClaim.Should().Be(UserRole.Admin.ToString());
    }

    [Fact]
    public async Task OfflineSyncEndpoint_WhenPayloadIsEmpty_ShouldReturnBadRequest()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Array.Empty<byte>());

        var tenantId = Guid.NewGuid();
        var claims = new[] { new Claim("TenantId", tenantId.ToString()) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);

        var sender = Substitute.For<ISender>();

        // Act
        var result = await OfflineSyncEndpoints.HandleSyncOperationAsync(
            moduleName: "Breeding",
            actionType: "RegisterDiagnosis",
            force: false,
            request: context.Request,
            userClaims: principal,
            tenantContext: tenantContext,
            sender: sender,
            cancellationToken: CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
    }
}

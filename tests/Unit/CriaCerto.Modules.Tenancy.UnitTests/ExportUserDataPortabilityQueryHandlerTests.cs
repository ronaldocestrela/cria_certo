using System.Text.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Features.ExportUserDataPortability;
using CriaCerto.Modules.Tenancy.Application.Features.ExportUserDataPortability.Dtos;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class ExportUserDataPortabilityQueryHandlerTests : IDisposable
{
    private readonly SqliteConnection _sqliteConnection;
    private readonly TenancyDbContext _dbContext;
    private readonly ITenancyAuditSink _auditSink;
    private readonly IUserAuditTrailLookupService _auditTrailLookupService;

    public ExportUserDataPortabilityQueryHandlerTests()
    {
        _sqliteConnection = new SqliteConnection("Filename=:memory:");
        _sqliteConnection.Open();

        var options = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseSqlite(_sqliteConnection)
            .Options;

        _dbContext = new TenancyDbContext(options);
        _dbContext.Database.EnsureCreated();

        _auditSink = Substitute.For<ITenancyAuditSink>();
        _auditTrailLookupService = Substitute.For<IUserAuditTrailLookupService>();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _sqliteConnection.Close();
        _sqliteConnection.Dispose();
    }

    [Fact]
    public async Task Handle_Should_Return_Portability_Package_And_Record_Audit_When_User_Exists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var clientIp = "192.168.1.100";

        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Fazenda Nelore Ouro",
            CNPJ = "98.765.432/0001-10",
            CnpjNormalized = "98765432000110",
            SubscribedPlan = "Pro",
            Status = "Active"
        };

        var user = new User
        {
            Id = userId,
            Email = "produtor@neloreouro.com.br",
            FullName = "Roberto Agro",
            PhoneNumber = "+5511988887777",
            PasswordHash = "SECRET_HASH_DO_NOT_EXPOSE",
            PasswordResetToken = "TOKEN_DO_NOT_EXPOSE",
            IsEmailConfirmed = true,
            EmailConfirmedAtUtc = DateTime.UtcNow.AddMonths(-2),
            CreatedAtUtc = DateTime.UtcNow.AddMonths(-3),
            TermsAcceptedAtUtc = DateTime.UtcNow.AddMonths(-3),
            PrivacyPolicyAcceptedAtUtc = DateTime.UtcNow.AddMonths(-3),
            TermsVersion = "2.0",
            PrivacyPolicyVersion = "2.0",
            ConsentIpAddress = "177.100.50.25"
        };

        var userTenant = new UserTenant
        {
            UserId = userId,
            User = user,
            TenantId = tenantId,
            Tenant = tenant,
            Role = UserRole.Admin,
            JoinedAt = DateTime.UtcNow.AddMonths(-3)
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        var mockTrail = new List<UserAuditTrailEntryExportDto>
        {
            new(DateTime.UtcNow.AddMonths(-3), "USER_CONSENT_REGISTERED", "Compliance", "High", "177.100.50.25", "Consent accepted"),
            new(DateTime.UtcNow.AddMonths(-1), "USER_PROFILE_RECTIFIED", "Compliance", "Medium", "177.100.50.25", "Phone updated")
        };

        _auditTrailLookupService
            .GetUserAuditTrailAsync(userId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(mockTrail);

        var handler = new ExportUserDataPortabilityQueryHandler(
            _dbContext,
            _auditSink,
            _auditTrailLookupService);

        var query = new ExportUserDataPortabilityQuery(userId, clientIp);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ContentType.Should().Be("application/json");
        result.Value.FileName.Should().StartWith($"meus-dados-lgpd-{userId}");
        result.Value.Content.Should().NotBeEmpty();

        var jsonString = System.Text.Encoding.UTF8.GetString(result.Value.Content);
        jsonString.Should().NotContain("SECRET_HASH_DO_NOT_EXPOSE");
        jsonString.Should().NotContain("TOKEN_DO_NOT_EXPOSE");

        var dossier = JsonSerializer.Deserialize<DataSubjectPortabilityDossierDto>(jsonString);
        dossier.Should().NotBeNull();
        dossier!.Specification.Should().Contain("LGPD");
        dossier.Purpose.Should().ContainEquivalentOf("portabilidade");
        dossier.UserProfile.Id.Should().Be(userId);
        dossier.UserProfile.FullName.Should().Be("Roberto Agro");
        dossier.UserProfile.Email.Should().Be("produtor@neloreouro.com.br");
        dossier.UserProfile.PhoneNumber.Should().Be("+5511988887777");
        dossier.UserProfile.IsEmailConfirmed.Should().BeTrue();
        dossier.ConsentHistory.TermsVersion.Should().Be("2.0");
        dossier.ConsentHistory.PrivacyPolicyVersion.Should().Be("2.0");
        dossier.AssociatedTenants.Should().HaveCount(1);
        dossier.AssociatedTenants[0].TenantName.Should().Be("Fazenda Nelore Ouro");
        dossier.AssociatedTenants[0].Role.Should().Be("Admin");
        dossier.AuditTrail.Should().HaveCount(2);

        await _auditSink.Received(1).RecordDataSubjectPortabilityExportAuditAsync(
            userId,
            "produtor@neloreouro.com.br",
            clientIp,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_Return_NotFound_When_User_Does_Not_Exist()
    {
        // Arrange
        var missingUserId = Guid.NewGuid();
        var handler = new ExportUserDataPortabilityQueryHandler(
            _dbContext,
            _auditSink,
            _auditTrailLookupService);

        var query = new ExportUserDataPortabilityQuery(missingUserId, "127.0.0.1");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("User.NotFound");

        await _auditSink.DidNotReceive().RecordDataSubjectPortabilityExportAuditAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }
}

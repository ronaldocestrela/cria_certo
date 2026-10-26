using CriaCerto.Api.Services;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CriaCerto.Architecture.IntegrationTests;

public class UserProfileAndSecurityAuditIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BackofficeDbContext _backofficeDbContext;

    public UserProfileAndSecurityAuditIntegrationTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<BackofficeDbContext>()
            .UseSqlite(_connection)
            .Options;

        _backofficeDbContext = new BackofficeDbContext(options);
        _backofficeDbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _backofficeDbContext.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task TenancyBackofficeAuditSink_Should_Create_Sha256_Audit_Record_For_Profile_Rectification()
    {
        // Arrange
        var sink = new TenancyBackofficeAuditSink(_backofficeDbContext, NullLogger<TenancyBackofficeAuditSink>.Instance);
        var userId = Guid.NewGuid();
        var email = "produtor.retificacao@criacerto.com.br";
        var oldName = "Nome Antigo";
        var newName = "Nome Retificado Conforme Art18";
        var oldPhone = "67988880000";
        var newPhone = "67999991111";
        var ip = "200.150.10.5";

        // Act
        await sink.RecordUserProfileUpdateAuditAsync(userId, email, oldName, newName, oldPhone, newPhone, ip);

        // Assert
        var savedAudit = await _backofficeDbContext.AuditLogs
            .FirstOrDefaultAsync(a => a.Action == "USER_PROFILE_RECTIFIED" && a.AdminUserEmail == email);

        savedAudit.Should().NotBeNull();
        savedAudit!.Category.Should().Be(AuditCategory.Compliance);
        savedAudit.Severity.Should().Be(AuditSeverity.Medium);
        savedAudit.Resource.Should().Be($"Users/{userId}");
        savedAudit.IpAddress.Should().Be(ip);
        savedAudit.ActorRole.Should().Be("RegisteredUser");
        savedAudit.RecordHash.Should().NotBeNullOrWhiteSpace();
        savedAudit.VerifyIntegrity().Should().BeTrue();
        savedAudit.OldValuesJson.Should().Contain(oldName);
        savedAudit.OldValuesJson.Should().Contain(oldPhone);
        savedAudit.NewValuesJson.Should().Contain(newName);
        savedAudit.NewValuesJson.Should().Contain(newPhone);
    }

    [Fact]
    public async Task TenancyBackofficeAuditSink_Should_Create_Sha256_Audit_Record_For_Password_Change()
    {
        // Arrange
        var sink = new TenancyBackofficeAuditSink(_backofficeDbContext, NullLogger<TenancyBackofficeAuditSink>.Instance);
        var userId = Guid.NewGuid();
        var email = "produtor.seguranca@criacerto.com.br";
        var ip = "200.150.10.6";

        // Act
        await sink.RecordUserPasswordChangeAuditAsync(userId, email, ip);

        // Assert
        var savedAudit = await _backofficeDbContext.AuditLogs
            .FirstOrDefaultAsync(a => a.Action == "USER_PASSWORD_CHANGED" && a.AdminUserEmail == email);

        savedAudit.Should().NotBeNull();
        savedAudit!.Category.Should().Be(AuditCategory.Security);
        savedAudit.Severity.Should().Be(AuditSeverity.High);
        savedAudit.Resource.Should().Be($"Users/{userId}");
        savedAudit.IpAddress.Should().Be(ip);
        savedAudit.ActorRole.Should().Be("RegisteredUser");
        savedAudit.RecordHash.Should().NotBeNullOrWhiteSpace();
        savedAudit.VerifyIntegrity().Should().BeTrue();
    }

    [Fact]
    public async Task TenancyBackofficeAuditSink_Should_Create_Sha256_Audit_Record_For_Data_Portability_Export()
    {
        // Arrange
        var sink = new TenancyBackofficeAuditSink(_backofficeDbContext, NullLogger<TenancyBackofficeAuditSink>.Instance);
        var userId = Guid.NewGuid();
        var email = "produtor.portabilidade@criacerto.com.br";
        var ip = "200.150.10.7";

        // Act
        await sink.RecordDataSubjectPortabilityExportAuditAsync(userId, email, ip);

        // Assert
        var savedAudit = await _backofficeDbContext.AuditLogs
            .FirstOrDefaultAsync(a => a.Action == "DATA_SUBJECT_PORTABILITY_EXPORTED" && a.AdminUserEmail == email);

        savedAudit.Should().NotBeNull();
        savedAudit!.Category.Should().Be(AuditCategory.Compliance);
        savedAudit.Severity.Should().Be(AuditSeverity.High);
        savedAudit.Resource.Should().Be($"Users/{userId}");
        savedAudit.IpAddress.Should().Be(ip);
        savedAudit.ActorRole.Should().Be("RegisteredUser");
        savedAudit.RecordHash.Should().NotBeNullOrWhiteSpace();
        savedAudit.VerifyIntegrity().Should().BeTrue();
        savedAudit.DetailsJson.Should().Contain("DataSubjectPortabilityExported_Art18_V");
    }

    [Fact]
    public async Task TenancyBackofficeAuditSink_Should_Retrieve_User_Audit_Trail_For_Portability()
    {
        // Arrange
        var sink = new TenancyBackofficeAuditSink(_backofficeDbContext, NullLogger<TenancyBackofficeAuditSink>.Instance);
        var userId = Guid.NewGuid();
        var email = "produtor.trilha@criacerto.com.br";
        var ip = "200.150.10.8";

        await sink.RecordUserConsentAuditAsync(userId, email, "1.0", "1.0", ip);
        await sink.RecordUserProfileUpdateAuditAsync(userId, email, "Nome Antigo", "Nome Novo", null, null, ip);

        // Act
        var trail = await sink.GetUserAuditTrailAsync(userId, limit: 10);

        // Assert
        trail.Should().NotBeNull();
        trail.Should().HaveCount(2);
        trail.Should().Contain(t => t.Action == "USER_CONSENT_REGISTERED");
        trail.Should().Contain(t => t.Action == "USER_PROFILE_RECTIFIED");
    }
}

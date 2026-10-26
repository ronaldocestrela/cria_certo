using CriaCerto.Api.Services;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Net;
using Xunit;

namespace CriaCerto.Architecture.IntegrationTests;

public class UserConsentAuditIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BackofficeDbContext _backofficeDbContext;

    public UserConsentAuditIntegrationTests()
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
    public async Task TenancyBackofficeAuditSink_Should_Create_Immutable_Sha256_Audit_Record()
    {
        // Arrange
        var sink = new TenancyBackofficeAuditSink(_backofficeDbContext, NullLogger<TenancyBackofficeAuditSink>.Instance);
        var userId = Guid.NewGuid();
        var email = "produtor.audit@criacerto.com.br";
        var termsVersion = "1.0";
        var privacyVersion = "1.0";
        var ip = "187.120.45.67";

        // Act
        await sink.RecordUserConsentAuditAsync(userId, email, termsVersion, privacyVersion, ip);

        // Assert
        var savedAudit = await _backofficeDbContext.AuditLogs
            .FirstOrDefaultAsync(a => a.Action == "USER_CONSENT_REGISTERED" && a.AdminUserEmail == email);

        savedAudit.Should().NotBeNull();
        savedAudit!.Category.Should().Be(AuditCategory.Compliance);
        savedAudit.Severity.Should().Be(AuditSeverity.High);
        savedAudit.Resource.Should().Be($"Users/{userId}");
        savedAudit.IpAddress.Should().Be(ip);
        savedAudit.ActorRole.Should().Be("RegisteredUser");
        savedAudit.RecordHash.Should().NotBeNullOrWhiteSpace();
        savedAudit.VerifyIntegrity().Should().BeTrue();
        savedAudit.DetailsJson.Should().Contain(termsVersion);
        savedAudit.DetailsJson.Should().Contain(privacyVersion);
        savedAudit.DetailsJson.Should().Contain(ip);
    }

    [Fact]
    public void HttpContextUserClientIpResolver_Should_Extract_Forwarded_For_Ip()
    {
        // Arrange
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Forwarded-For"] = "198.51.100.77, 10.0.0.1";
        httpContextAccessor.HttpContext.Returns(httpContext);

        var resolver = new HttpContextUserClientIpResolver(httpContextAccessor);

        // Act
        var ip = resolver.ResolveClientIp();

        // Assert
        ip.Should().Be("198.51.100.77");
    }

    [Fact]
    public void HttpContextUserClientIpResolver_Should_Fallback_To_RemoteIpAddress()
    {
        // Arrange
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("177.136.241.10");
        httpContextAccessor.HttpContext.Returns(httpContext);

        var resolver = new HttpContextUserClientIpResolver(httpContextAccessor);

        // Act
        var ip = resolver.ResolveClientIp();

        // Assert
        ip.Should().Be("177.136.241.10");
    }
}

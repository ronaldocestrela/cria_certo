using System.Reflection;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Domain;

public class AuditLogDomainTests
{
    [Fact]
    public void Create_LegacyOverload_ShouldCreateLogWithDefaultsAndValidHash()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var adminEmail = "admin@criacerto.com.br";
        var action = "PlanCatalog.Created";
        var resource = "PlanCatalog/starter";
        var ipAddress = "192.168.1.10";
        var detailsJson = "{\"catalogCode\":\"starter\"}";

        // Act
        var log = AuditLog.Create(adminUserId, adminEmail, action, resource, ipAddress, detailsJson);

        // Assert
        log.Should().NotBeNull();
        log.Id.Should().NotBeEmpty();
        log.AdminUserId.Should().Be(adminUserId);
        log.AdminUserEmail.Should().Be(adminEmail);
        log.Action.Should().Be(action);
        log.Resource.Should().Be(resource);
        log.IpAddress.Should().Be(ipAddress);
        log.DetailsJson.Should().Be(detailsJson);
        log.Severity.Should().Be(AuditSeverity.Medium);
        log.Category.Should().Be(AuditCategory.PlanCatalog);
        log.RecordHash.Should().NotBeNullOrWhiteSpace();
        log.IsArchived.Should().BeFalse();
        log.VerifyIntegrity().Should().BeTrue();
    }

    [Fact]
    public void CreateForensic_WithAllParameters_ShouldPopulateFieldsAndComputeValidHash()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var adminEmail = "admin@criacerto.com.br";
        var actorRole = "PlatformOwner";
        var action = "Tenant.Suspended";
        var resource = "Tenant/santa-maria";
        var targetTenantId = Guid.NewGuid();
        var targetTenantName = "Fazenda Santa Maria";
        var category = AuditCategory.TenantManagement;
        var severity = AuditSeverity.Critical;
        var ipAddress = "200.180.10.5";
        var userAgent = "Mozilla/5.0 (X11; Linux x86_64)";
        var oldValuesJson = "{\"status\":\"Active\"}";
        var newValuesJson = "{\"status\":\"Suspended\",\"reason\":\"Inadimplência\"}";
        var previousHash = "hash-123456";

        // Act
        var log = AuditLog.CreateForensic(
            adminUserId,
            adminEmail,
            actorRole,
            action,
            category,
            severity,
            resource,
            targetTenantId,
            targetTenantName,
            ipAddress,
            userAgent,
            oldValuesJson,
            newValuesJson,
            previousHash);

        // Assert
        log.Should().NotBeNull();
        log.Id.Should().NotBeEmpty();
        log.AdminUserId.Should().Be(adminUserId);
        log.AdminUserEmail.Should().Be(adminEmail);
        log.ActorRole.Should().Be(actorRole);
        log.Action.Should().Be(action);
        log.Category.Should().Be(category);
        log.Severity.Should().Be(severity);
        log.Resource.Should().Be(resource);
        log.TargetTenantId.Should().Be(targetTenantId);
        log.TargetTenantName.Should().Be(targetTenantName);
        log.IpAddress.Should().Be(ipAddress);
        log.UserAgent.Should().Be(userAgent);
        log.OldValuesJson.Should().Be(oldValuesJson);
        log.NewValuesJson.Should().Be(newValuesJson);
        log.PreviousRecordHash.Should().Be(previousHash);
        log.RecordHash.Should().NotBeNullOrWhiteSpace();
        log.VerifyIntegrity().Should().BeTrue();
    }

    [Fact]
    public void VerifyIntegrity_WhenActionOrIpIsTampered_ShouldReturnFalse()
    {
        // Arrange
        var log = AuditLog.CreateForensic(
            Guid.NewGuid(),
            "admin@criacerto.com.br",
            "PlatformOwner",
            "Tenant.Suspended",
            AuditCategory.TenantManagement,
            AuditSeverity.Critical,
            "Tenant/123",
            Guid.NewGuid(),
            "Fazenda Teste",
            "192.168.1.1",
            "Chrome",
            "{}",
            "{}",
            "prev-hash");

        log.VerifyIntegrity().Should().BeTrue();

        // Act: tamper property via reflection simulating direct DB alteration
        typeof(AuditLog).GetProperty(nameof(AuditLog.IpAddress))!
            .SetValue(log, "10.0.0.99");

        // Assert
        log.VerifyIntegrity().Should().BeFalse();
    }

    [Fact]
    public void VerifyIntegrity_WhenOldValuesOrNewValuesTampered_ShouldReturnFalse()
    {
        // Arrange
        var log = AuditLog.CreateForensic(
            Guid.NewGuid(),
            "admin@criacerto.com.br",
            "PlatformOwner",
            "PlanVersion.Published",
            AuditCategory.PlanCatalog,
            AuditSeverity.High,
            "Plan/1",
            null,
            null,
            "192.168.1.1",
            "Firefox",
            "{\"price\":100}",
            "{\"price\":200}",
            null);

        log.VerifyIntegrity().Should().BeTrue();

        // Act: Tamper new values
        typeof(AuditLog).GetProperty(nameof(AuditLog.NewValuesJson))!
            .SetValue(log, "{\"price\":50}");

        // Assert
        log.VerifyIntegrity().Should().BeFalse();
    }

    [Fact]
    public void MarkAsArchived_ShouldSetIsArchivedToTrue()
    {
        // Arrange
        var log = AuditLog.Create(Guid.NewGuid(), "admin@criacerto.com.br", "User.Login", "User/1", "127.0.0.1");

        // Act
        log.MarkAsArchived();

        // Assert
        log.IsArchived.Should().BeTrue();
    }
}

using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Domain;

public class ImpersonationSessionDomainTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldCreateActiveSession()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var adminEmail = "admin@criacerto.com.br";
        var tenantId = Guid.NewGuid();
        var tenantName = "Fazenda Santa Maria";
        var targetUserId = Guid.NewGuid();
        var targetEmail = "gestor@santamaria.com.br";
        var ticket = "SUP-1042";
        var justification = "Verificação de divergência no cálculo do ganho de peso médio (GPD) do lote 12.";
        var durationMinutes = 15;
        var ipAddress = "192.168.1.100";
        var userAgent = "Mozilla/5.0 Chrome/120.0";

        // Act
        var session = ImpersonationSession.Create(
            adminUserId,
            adminEmail,
            tenantId,
            tenantName,
            targetUserId,
            targetEmail,
            ticket,
            justification,
            durationMinutes,
            ipAddress,
            userAgent);

        // Assert
        session.Should().NotBeNull();
        session.Id.Should().NotBeEmpty();
        session.AdminUserId.Should().Be(adminUserId);
        session.AdminUserEmail.Should().Be(adminEmail);
        session.TargetTenantId.Should().Be(tenantId);
        session.TargetTenantName.Should().Be(tenantName);
        session.TargetUserId.Should().Be(targetUserId);
        session.TargetUserEmail.Should().Be(targetEmail);
        session.SupportTicket.Should().Be(ticket);
        session.Justification.Should().Be(justification);
        session.Status.Should().Be(ImpersonationSessionStatus.Active);
        session.IpAddress.Should().Be(ipAddress);
        session.UserAgent.Should().Be(userAgent);
        session.StartedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        session.ExpiresAtUtc.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(2));
        session.EndedAtUtc.Should().BeNull();
        session.IsActive().Should().BeTrue();
        session.GetRemainingSeconds().Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(1, 5)]    // Less than min (5 min) -> clamped to 5
    [InlineData(0, 5)]    // 0 -> clamped to 5
    [InlineData(-10, 5)]  // Negative -> clamped to 5
    [InlineData(120, 60)] // Greater than max (60 min) -> clamped to 60
    public void Create_WithOutOfBoundDuration_ShouldClampToValidRange(int inputDuration, int expectedClamped)
    {
        // Act
        var session = ImpersonationSession.Create(
            Guid.NewGuid(),
            "admin@criacerto.com.br",
            Guid.NewGuid(),
            "Fazenda Esperança",
            null,
            null,
            "SUP-9999",
            "Análise emergencial de sincronização offline.",
            inputDuration,
            "127.0.0.1",
            "TestAgent");

        // Assert
        session.DurationMinutes.Should().Be(expectedClamped);
        session.ExpiresAtUtc.Should().BeCloseTo(session.StartedAtUtc.AddMinutes(expectedClamped), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void End_WhenSessionIsActive_ShouldSetStatusEndedAndEndedAtUtc()
    {
        // Arrange
        var session = ImpersonationSession.Create(
            Guid.NewGuid(), "admin@criacerto.com.br", Guid.NewGuid(), "Fazenda Boa Vista",
            null, null, "SUP-1010", "Verificação técnica.", 20, "127.0.0.1", "TestAgent");

        // Act
        session.End();

        // Assert
        session.Status.Should().Be(ImpersonationSessionStatus.Ended);
        session.EndedAtUtc.Should().NotBeNull();
        session.EndedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        session.IsActive().Should().BeFalse();
        session.GetRemainingSeconds().Should().Be(0);
    }

    [Fact]
    public void Revoke_WhenCalled_ShouldSetStatusRevoked()
    {
        // Arrange
        var session = ImpersonationSession.Create(
            Guid.NewGuid(), "admin@criacerto.com.br", Guid.NewGuid(), "Fazenda Boa Vista",
            null, null, "SUP-1010", "Verificação técnica.", 20, "127.0.0.1", "TestAgent");

        // Act
        session.Revoke("Revogado por encerramento de sessão do operador");

        // Assert
        session.Status.Should().Be(ImpersonationSessionStatus.Revoked);
        session.EndedAtUtc.Should().NotBeNull();
        session.IsActive().Should().BeFalse();
    }
}

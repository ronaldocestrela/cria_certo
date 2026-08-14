using CriaCerto.Modules.Tenancy.Application.Domain;
using FluentAssertions;

namespace CriaCerto.Modules.Tenancy.UnitTests.Domain;

public class TenantDomainTests
{
    private static Tenant CreateTenant(string status = "Active", bool isProtected = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Teste",
            CNPJ = "12.345.678/0001-90",
            CnpjNormalized = "12345678000190",
            Status = status,
            IsProtected = isProtected
        };

    [Fact]
    public void ChangeStatus_WhenValidTransition_ShouldUpdateStatusAndReason()
    {
        var tenant = CreateTenant("Active");
        var reason = "Inadimplência recorrente confirmada pelo financeiro.";

        var result = tenant.ChangeStatus(TenantStatus.Suspended, reason);

        result.IsSuccess.Should().BeTrue();
        tenant.Status.Should().Be("Suspended");
        tenant.StatusReason.Should().Be(reason);
        tenant.StatusChangedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void ChangeStatus_WhenInvalidTransition_ShouldReturnFailure()
    {
        var tenant = CreateTenant("Archived");

        var result = tenant.ChangeStatus(TenantStatus.Active, "Tentativa inválida de reativação.");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.InvalidTransition");
    }

    [Fact]
    public void ChangeStatus_WhenSameStatus_ShouldReturnFailure()
    {
        var tenant = CreateTenant("Active");

        var result = tenant.ChangeStatus(TenantStatus.Active, "Justificativa válida com texto suficiente.");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.AlreadyInStatus");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Curto demais")]
    public void ChangeStatus_WhenJustificationInvalid_ShouldReturnFailure(string? reason)
    {
        var tenant = CreateTenant("Active");

        var result = tenant.ChangeStatus(TenantStatus.Suspended, reason!);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.JustificationRequired");
    }

    [Fact]
    public void ChangeStatus_WhenProtectedTenant_ShouldBlockSuspend()
    {
        var tenant = CreateTenant("Active", isProtected: true);

        var result = tenant.ChangeStatus(TenantStatus.Suspended, "Tentativa de suspensão em tenant protegido.");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.ProtectedTenant");
    }

    [Fact]
    public void SetProtection_WhenValidReason_ShouldUpdateFlag()
    {
        var tenant = CreateTenant("Active");

        var result = tenant.SetProtection(true, "Tenant estratégico da plataforma interna.");

        result.IsSuccess.Should().BeTrue();
        tenant.IsProtected.Should().BeTrue();
    }

    [Fact]
    public void SetProtection_WhenSameValue_ShouldReturnFailure()
    {
        var tenant = CreateTenant("Active");
        tenant.IsProtected = true;

        var result = tenant.SetProtection(true, "Justificativa válida com texto suficiente.");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.AlreadyProtected");
    }
}

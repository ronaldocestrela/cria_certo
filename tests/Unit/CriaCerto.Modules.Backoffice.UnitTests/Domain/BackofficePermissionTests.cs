using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Security;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Domain;

public class BackofficePermissionTests
{
    [Fact]
    public void Create_WithValidData_ShouldReturnSuccess()
    {
        // Act
        var result = Permission.Create(BackofficePermissions.TenantsRead, "Leitura de tenants", BackofficePermissions.ScopeGlobal);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be(BackofficePermissions.TenantsRead);
        result.Value.Scope.Should().Be(BackofficePermissions.ScopeGlobal);
    }

    [Fact]
    public void Create_WithInvalidScope_ShouldReturnFailure()
    {
        // Act
        var result = Permission.Create(BackofficePermissions.TenantsRead, "Leitura de tenants", "ScopeInvalido");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Backoffice.InvalidScopeData");
    }

    [Fact]
    public void AdminRole_AddAndRemovePermission_ShouldManagePermissionsCorrectly()
    {
        // Arrange
        var role = AdminRole.Create("SupportN1", "Suporte N1").Value;
        var perm = Permission.Create(BackofficePermissions.TenantsRead, "Leitura", BackofficePermissions.ScopeGlobal).Value;

        // Act
        var addResult = role.AddPermission(perm);
        addResult.IsSuccess.Should().BeTrue();
        role.Permissions.Should().HaveCount(1);

        var removeResult = role.RemovePermission(BackofficePermissions.TenantsRead, BackofficePermissions.ScopeGlobal);

        // Assert
        removeResult.IsSuccess.Should().BeTrue();
        role.Permissions.Should().BeEmpty();
    }

    [Fact]
    public void BackofficePermissions_AllPermissions_ShouldIncludeCompliancePermissions()
    {
        // Assert
        BackofficePermissions.AllPermissions.Should().Contain(new[]
        {
            BackofficePermissions.ComplianceRead,
            BackofficePermissions.ComplianceExport,
            BackofficePermissions.ComplianceUnmask
        });
    }

    [Fact]
    public void BackofficeRoles_ReadOnlyAuditor_ShouldHaveComplianceReadAndExport_ButNotUnmask()
    {
        // Act
        var permissions = BackofficeRoles.GetDefaultPermissionsForRole(BackofficeRoles.ReadOnlyAuditor);

        // Assert
        permissions.Should().Contain(BackofficePermissions.ComplianceRead);
        permissions.Should().Contain(BackofficePermissions.ComplianceExport);
        permissions.Should().NotContain(BackofficePermissions.ComplianceUnmask);
    }

    [Fact]
    public void BackofficeRoles_SupportN1_ShouldNotHaveAnyCompliancePermissions()
    {
        // Act
        var permissions = BackofficeRoles.GetDefaultPermissionsForRole(BackofficeRoles.SupportN1);

        // Assert
        permissions.Should().NotContain(BackofficePermissions.ComplianceRead);
        permissions.Should().NotContain(BackofficePermissions.ComplianceExport);
        permissions.Should().NotContain(BackofficePermissions.ComplianceUnmask);
    }
}

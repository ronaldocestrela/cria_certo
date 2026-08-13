using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Domain;

public class AdminUserTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldReturnSuccess()
    {
        // Arrange
        var name = "Admin Master";
        var email = "admin@criacerto.com.br";
        var passwordHash = "hashed_secret_password";

        // Act
        var result = AdminUser.Create(name, email, passwordHash);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be(name);
        result.Value.Email.Should().Be(email);
        result.Value.IsActive.Should().BeTrue();
        result.Value.MfaEnabled.Should().BeFalse();
    }

    [Theory]
    [InlineData("", "admin@criacerto.com.br", "hash")]
    [InlineData("Admin", "", "hash")]
    [InlineData("Admin", "invalid-email", "hash")]
    public void Create_WithInvalidParameters_ShouldReturnFailure(string name, string email, string passwordHash)
    {
        // Act
        var result = AdminUser.Create(name, email, passwordHash);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BackofficeErrors.InvalidAdminUserData);
    }

    [Fact]
    public void Deactivate_WhenActive_ShouldDeactivateUser()
    {
        // Arrange
        var user = AdminUser.Create("Support N1", "support@criacerto.com.br", "hash").Value;

        // Act
        var result = user.Deactivate();

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.IsActive.Should().BeFalse();
    }

    [Fact]
    public void AssignRole_WithValidRole_ShouldAddRoleToUser()
    {
        // Arrange
        var user = AdminUser.Create("Platform Owner", "owner@criacerto.com.br", "hash").Value;
        var role = AdminRole.Create("PlatformOwner", "Full platform administrative access").Value;

        // Act
        var result = user.AssignRole(role);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.Roles.Should().Contain(r => r.Name == "PlatformOwner");
    }

    [Fact]
    public void RequiresMfa_WhenUserHasSensitivePermission_ShouldReturnTrue()
    {
        // Arrange
        var user = AdminUser.Create("Platform Owner", "owner@criacerto.com.br", "hash").Value;
        var role = AdminRole.Create("PlatformOwner", "Description").Value;
        var perm = Permission.Create("plans.publish", "Publish plans", "Global").Value;
        role.AddPermission(perm);
        user.AssignRole(role);

        // Act
        var requiresMfa = user.RequiresMfa();

        // Assert
        requiresMfa.Should().BeTrue();
    }

    [Fact]
    public void RequiresMfa_WhenUserHasOnlyNonSensitivePermissions_ShouldReturnFalse()
    {
        // Arrange
        var user = AdminUser.Create("Support N1", "support@criacerto.com.br", "hash").Value;
        var role = AdminRole.Create("SupportN1", "Description").Value;
        var perm = Permission.Create("tenants.read", "Read tenants", "Global").Value;
        role.AddPermission(perm);
        user.AssignRole(role);

        // Act
        var requiresMfa = user.RequiresMfa();

        // Assert
        requiresMfa.Should().BeFalse();
    }

    [Fact]
    public void EnableMfa_WithValidParameters_ShouldEnableMfaAndSetSecret()
    {
        // Arrange
        var user = AdminUser.Create("Admin", "admin@criacerto.com.br", "hash").Value;
        var secret = "JBSWY3DPEHPK3PXP";
        var recoveryCodes = new List<string> { "CODE1", "CODE2" };

        // Act
        var result = user.EnableMfa(secret, recoveryCodes);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.MfaEnabled.Should().BeTrue();
        user.MfaSecretKey.Should().Be(secret);
        user.RecoveryCodes.Should().BeEquivalentTo(recoveryCodes);
    }

    [Fact]
    public void DisableMfa_WhenEnabled_ShouldDisableMfaAndClearSecret()
    {
        // Arrange
        var user = AdminUser.Create("Admin", "admin@criacerto.com.br", "hash").Value;
        user.EnableMfa("JBSWY3DPEHPK3PXP", new List<string> { "CODE1" });

        // Act
        var result = user.DisableMfa();

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.MfaEnabled.Should().BeFalse();
        user.MfaSecretKey.Should().BeNull();
        user.RecoveryCodes.Should().BeEmpty();
    }

    [Fact]
    public void UpdatePasswordHash_WithValidHash_ShouldUpdateHashAndClearMustChangeFlag()
    {
        // Arrange
        var user = AdminUser.Create("Admin", "admin@criacerto.com.br", "old_hash").Value;
        var newHash = "new_hashed_password";

        // Act
        var result = user.UpdatePasswordHash(newHash);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.PasswordHash.Should().Be(newHash);
        user.MustChangePasswordOnNextLogin.Should().BeFalse();
    }

    [Fact]
    public void RemoveRole_WhenRoleAssigned_ShouldRemoveRole()
    {
        // Arrange
        var user = AdminUser.Create("Admin", "admin@criacerto.com.br", "hash").Value;
        var role = AdminRole.Create("SupportN1", "Description").Value;
        user.AssignRole(role);

        // Act
        var result = user.RemoveRole(role.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.Roles.Should().NotContain(r => r.Id == role.Id);
    }
}

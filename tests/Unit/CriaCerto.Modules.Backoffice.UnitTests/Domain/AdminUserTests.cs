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
}

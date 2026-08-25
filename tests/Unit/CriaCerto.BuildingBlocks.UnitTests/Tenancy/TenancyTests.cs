using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.BuildingBlocks.Abstractions.Tenancy;
using CriaCerto.BuildingBlocks.Infrastructure.Tenancy;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Features.Login;
using CriaCerto.Modules.Tenancy.Application.Features.SelectTenant;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace CriaCerto.BuildingBlocks.UnitTests.Tenancy;

public class TenancyTests : IDisposable
{
    private readonly SqliteConnection _sqliteConnection;
    private readonly TenancyDbContext _dbContext;
    private readonly IJwtService _jwtService;

    public TenancyTests()
    {
        _sqliteConnection = new SqliteConnection("Filename=:memory:");
        _sqliteConnection.Open();

        var options = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseSqlite(_sqliteConnection)
            .Options;

        _dbContext = new TenancyDbContext(options);
        _dbContext.Database.EnsureCreated();

        _jwtService = Substitute.For<IJwtService>();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _sqliteConnection.Close();
        _sqliteConnection.Dispose();
    }

    [Fact]
    public void PasswordHasher_ShouldHashAndVerifyPasswordCorrectly()
    {
        // Arrange
        var password = "StrongPassword123!";

        // Act
        var hash = PasswordHasher.Hash(password);
        var verifySuccess = PasswordHasher.Verify(password, hash);
        var verifyFailure = PasswordHasher.Verify("WrongPassword", hash);

        // Assert
        hash.Should().NotBeNullOrEmpty();
        verifySuccess.Should().BeTrue();
        verifyFailure.Should().BeFalse();
    }

    [Fact]
    public void TenantConnectionProvider_ShouldResolveFoundationDb_WhenTenantIdIsNull()
    {
        // Arrange
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns((Guid?)null);

        var configuration = Substitute.For<IConfiguration>();
        configuration.GetConnectionString("SqlServer")
            .Returns("Server=localhost;Database=criacerto_foundation;User Id=sa;Password=CriaCerto@123;TrustServerCertificate=True;Encrypt=False");

        var provider = new TenantConnectionProvider(tenantContext, configuration);

        // Act
        var connectionString = provider.GetConnectionString();

        // Assert
        new SqlConnectionStringBuilder(connectionString).InitialCatalog
            .Should().Be("criacerto_foundation");
    }

    [Fact]
    public void TenantConnectionProvider_ShouldResolveTenantDb_WhenTenantIdIsNotNull()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);

        var configuration = Substitute.For<IConfiguration>();
        configuration.GetConnectionString("SqlServer")
            .Returns("Server=localhost;User Id=sa;Password=CriaCerto@123;TrustServerCertificate=True;Encrypt=False");

        var provider = new TenantConnectionProvider(tenantContext, configuration);

        // Act
        var connectionString = provider.GetConnectionString();

        // Assert
        connectionString.Should().Contain($"Initial Catalog=criacerto_tenant_{tenantId:N}");
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ShouldReturnUnauthorized()
    {
        // Arrange
        var handler = new LoginCommandHandler(_dbContext, _jwtService);
        var command = new LoginCommand("wrong@email.com", "anypassword");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Login_WithSingleTenantUser_ShouldDirectlyReturnToken()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user1@criacerto.com",
            FullName = "User One",
            PasswordHash = PasswordHasher.Hash("Pass123!")
        };
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Granja 1",
            CNPJ = "11111111111",
            State = "RS",
            Type = "Creche"
        };
        var userTenant = new UserTenant { UserId = user.Id, TenantId = tenant.Id };

        _dbContext.Users.Add(user);
        _dbContext.Tenants.Add(tenant);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        _jwtService.GenerateToken(Arg.Any<User>(), Arg.Any<Tenant>()).Returns("fake-jwt-token");

        var handler = new LoginCommandHandler(_dbContext, _jwtService);
        var command = new LoginCommand(user.Email, "Pass123!");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RequiresTenantSelection.Should().BeFalse();
        result.Value.Token.Should().Be("fake-jwt-token");
    }

    [Fact]
    public async Task Login_WithMultiTenantUser_ShouldRequireTenantSelection()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user2@criacerto.com",
            FullName = "User Two",
            PasswordHash = PasswordHasher.Hash("Pass123!")
        };
        var tenant1 = new Tenant { Id = Guid.NewGuid(), Name = "Granja Santa Fé", CNPJ = "1", CnpjNormalized = "1", State = "RS", Type = "Matriz" };
        var tenant2 = new Tenant { Id = Guid.NewGuid(), Name = "Unidade Terminação III", CNPJ = "2", CnpjNormalized = "2", State = "SC", Type = "Engorda" };

        _dbContext.Users.Add(user);
        _dbContext.Tenants.AddRange(tenant1, tenant2);
        _dbContext.UserTenants.AddRange(
            new UserTenant { UserId = user.Id, TenantId = tenant1.Id },
            new UserTenant { UserId = user.Id, TenantId = tenant2.Id }
        );
        await _dbContext.SaveChangesAsync();

        var handler = new LoginCommandHandler(_dbContext, _jwtService);
        var command = new LoginCommand(user.Email, "Pass123!");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RequiresTenantSelection.Should().BeTrue();
        result.Value.Token.Should().BeNull();
        result.Value.AvailableTenants.Should().HaveCount(2);
        result.Value.AvailableTenants.Select(t => t.Name).Should().Contain(new[] { "Granja Santa Fé", "Unidade Terminação III" });
    }

    [Fact]
    public async Task SelectTenant_WithAuthorizedTenant_ShouldSucceedAndReturnToken()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user3@criacerto.com",
            FullName = "User Three",
            PasswordHash = PasswordHasher.Hash("Pass123!")
        };
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Granja 3", CNPJ = "3", State = "PR", Type = "Gestação" };
        var userTenant = new UserTenant { UserId = user.Id, TenantId = tenant.Id };

        _dbContext.Users.Add(user);
        _dbContext.Tenants.Add(tenant);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        _jwtService.GenerateToken(Arg.Any<User>(), Arg.Any<Tenant>()).Returns("fake-jwt-token-selected");

        var handler = new SelectTenantCommandHandler(_dbContext, _jwtService);
        var command = new SelectTenantCommand(user.Id, tenant.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Token.Should().Be("fake-jwt-token-selected");
    }

    [Fact]
    public async Task SelectTenant_WithUnauthorizedTenant_ShouldFail()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user4@criacerto.com",
            FullName = "User Four",
            PasswordHash = PasswordHasher.Hash("Pass123!")
        };
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Granja 4", CNPJ = "4" };

        _dbContext.Users.Add(user);
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var handler = new SelectTenantCommandHandler(_dbContext, _jwtService);
        var command = new SelectTenantCommand(user.Id, tenant.Id); // No UserTenant map

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
    }
}

using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Domain.Errors;
using CriaCerto.Modules.Tenancy.Application.Features.RefreshToken;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class RefreshTokenCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _sqliteConnection;
    private readonly TenancyDbContext _dbContext;
    private readonly FakeJwtService _jwtService;

    public RefreshTokenCommandHandlerTests()
    {
        _sqliteConnection = new SqliteConnection("Filename=:memory:");
        _sqliteConnection.Open();

        var options = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseSqlite(_sqliteConnection)
            .Options;

        _dbContext = new TenancyDbContext(options);
        _dbContext.Database.EnsureCreated();

        _jwtService = new FakeJwtService();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _sqliteConnection.Close();
        _sqliteConnection.Dispose();
    }

    [Fact]
    public async Task Handle_Should_Return_New_Token_With_Current_Tenant_Plan_When_Valid()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Primavera",
            CNPJ = "12.345.678/0001-90",
            Status = "Active",
            SubscribedPlan = "Enterprise"
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Carlos Alberto",
            Email = "carlos@primavera.com",
            PasswordHash = "hash123"
        };

        var userTenant = new UserTenant
        {
            UserId = user.Id,
            TenantId = tenant.Id,
            Role = UserRole.Admin,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        var handler = new RefreshTokenCommandHandler(_dbContext, _jwtService);
        var command = new RefreshTokenCommand(user.Id, tenant.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Token.Should().Be("refreshed-jwt-token-Enterprise");
        result.Value.TenantId.Should().Be(tenant.Id);
        result.Value.SubscribedPlan.Should().Be("Enterprise");
        result.Value.Role.Should().Be(UserRole.Admin.ToString());
    }

    [Fact]
    public async Task Handle_Should_Fail_When_User_Not_Found()
    {
        // Arrange
        var handler = new RefreshTokenCommandHandler(_dbContext, _jwtService);
        var command = new RefreshTokenCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("User.NotFound");
    }

    [Fact]
    public async Task Handle_Should_Fail_When_User_Not_Member_Of_Tenant()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Bela Vista",
            CNPJ = "98.765.432/0001-10",
            Status = "Active",
            SubscribedPlan = "Pro"
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Renato Castro",
            Email = "renato@outro.com",
            PasswordHash = "hash123"
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new RefreshTokenCommandHandler(_dbContext, _jwtService);
        var command = new RefreshTokenCommand(user.Id, tenant.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.UnauthorizedTenant");
    }

    [Theory]
    [InlineData("Suspended")]
    [InlineData("Cancelled")]
    [InlineData("Archived")]
    public async Task Handle_Should_Fail_When_Tenant_Is_Not_Accessible(string blockedStatus)
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Bloqueada",
            CNPJ = "55.555.555/0001-55",
            Status = blockedStatus,
            SubscribedPlan = "Pro"
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Pedro Souza",
            Email = "pedro@bloqueada.com",
            PasswordHash = "hash123"
        };

        var userTenant = new UserTenant
        {
            UserId = user.Id,
            TenantId = tenant.Id,
            Role = UserRole.Admin,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        var handler = new RefreshTokenCommandHandler(_dbContext, _jwtService);
        var command = new RefreshTokenCommand(user.Id, tenant.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(TenancyErrors.TenantNotAccessible.Code);
    }

    private sealed class FakeJwtService : IJwtService
    {
        public string GenerateToken(User user, Tenant tenant, UserRole role = UserRole.Admin)
        {
            return $"refreshed-jwt-token-{tenant.SubscribedPlan}";
        }
    }
}

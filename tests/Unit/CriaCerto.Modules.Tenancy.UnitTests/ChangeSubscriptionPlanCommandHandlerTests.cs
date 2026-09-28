using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Features.ChangeSubscriptionPlan;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class ChangeSubscriptionPlanCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _sqliteConnection;
    private readonly TenancyDbContext _dbContext;
    private readonly IJwtService _jwtService;

    public ChangeSubscriptionPlanCommandHandlerTests()
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
    public async Task Handle_Should_Update_SubscribedPlan_And_Generate_Token()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Sol Nascente",
            CNPJ = "11.111.111/0001-11",
            Status = "Active",
            SubscribedPlan = "Starter"
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "João Silva",
            Email = "joao@solnascente.com",
            PasswordHash = "hash"
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

        var handler = new ChangeSubscriptionPlanCommandHandler(_dbContext, _jwtService);
        var command = new ChangeSubscriptionPlanCommand(tenant.Id, user.Id, "Pro");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Token.Should().Be("mock-new-jwt-token");
        result.Value.Profile.SubscribedPlan.Should().Be("Pro");

        var tenantInDb = await _dbContext.Tenants.FindAsync(tenant.Id);
        tenantInDb.Should().NotBeNull();
        tenantInDb!.SubscribedPlan.Should().Be("Pro");
    }

    [Fact]
    public async Task Handle_Should_Fail_When_Tenant_Not_Found()
    {
        // Arrange
        var handler = new ChangeSubscriptionPlanCommandHandler(_dbContext, _jwtService);
        var command = new ChangeSubscriptionPlanCommand(Guid.NewGuid(), Guid.NewGuid(), "Pro");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.NotFound");
    }

    [Fact]
    public async Task Handle_Should_Fail_When_User_Not_Member_Of_Tenant()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Vista Alegre",
            CNPJ = "22.222.222/0001-22",
            Status = "Active",
            SubscribedPlan = "Starter"
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Maria Souza",
            Email = "maria@outra.com",
            PasswordHash = "hash"
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new ChangeSubscriptionPlanCommandHandler(_dbContext, _jwtService);
        var command = new ChangeSubscriptionPlanCommand(tenant.Id, user.Id, "Enterprise");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.UnauthorizedTenant");
    }

    [Theory]
    [InlineData(UserRole.Veterinario)]
    [InlineData(UserRole.OperadorCurral)]
    public async Task Handle_Should_Fail_When_User_Is_Not_Admin(UserRole nonAdminRole)
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Ouro Verde",
            CNPJ = "33.333.333/0001-33",
            Status = "Active",
            SubscribedPlan = "Starter"
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Carlos Peão",
            Email = "carlos@ouroverde.com",
            PasswordHash = "hash"
        };

        var userTenant = new UserTenant
        {
            UserId = user.Id,
            TenantId = tenant.Id,
            Role = nonAdminRole,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        _dbContext.UserTenants.Add(userTenant);
        await _dbContext.SaveChangesAsync();

        var handler = new ChangeSubscriptionPlanCommandHandler(_dbContext, _jwtService);
        var command = new ChangeSubscriptionPlanCommand(tenant.Id, user.Id, "Enterprise");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.ForbiddenPlanChange");
    }

    private sealed class FakeJwtService : IJwtService
    {
        public string GenerateToken(User user, Tenant tenant, UserRole role = UserRole.Admin)
        {
            return "mock-new-jwt-token";
        }
    }
}

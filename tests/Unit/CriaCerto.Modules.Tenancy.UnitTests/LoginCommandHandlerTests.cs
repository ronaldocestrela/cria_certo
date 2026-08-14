using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Features.Login;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class LoginCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TenancyDbContext _dbContext;

    public LoginCommandHandlerTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<TenancyDbContext>().UseSqlite(_connection).Options;
        _dbContext = new TenancyDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task Handle_Should_Block_Login_When_Only_Tenant_Is_Suspended()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            FullName = "Produtor",
            Email = "produtor@test.com",
            PasswordHash = PasswordHasher.Hash("Password123!")
        };
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Fazenda Suspensa",
            CNPJ = "12.345.678/0001-90",
            CnpjNormalized = "12345678000190",
            Status = TenantLifecycle.ToStatusString(TenantStatus.Suspended),
            SubscribedPlan = "Starter",
            Capacity = 500,
            State = "MT",
            City = "Sinop",
            StateRegistration = "IE",
            Type = "Corte",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        _dbContext.Tenants.Add(tenant);
        _dbContext.UserTenants.Add(new UserTenant
        {
            UserId = userId,
            TenantId = tenantId,
            Role = UserRole.Admin,
            JoinedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var handler = new LoginCommandHandler(_dbContext, new FakeJwtService());
        var result = await handler.Handle(new LoginCommand("produtor@test.com", "Password123!"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.NotAccessible");
    }

    private sealed class FakeJwtService : IJwtService
    {
        public string GenerateToken(User user, Tenant tenant, UserRole role) => "token";
    }
}

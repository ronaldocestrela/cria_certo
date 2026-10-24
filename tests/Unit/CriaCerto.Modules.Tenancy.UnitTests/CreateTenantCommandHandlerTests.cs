using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Features.CreateTenant;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class CreateTenantCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _sqliteConnection;
    private readonly TenancyDbContext _dbContext;
    private readonly IJwtService _jwtService;

    public CreateTenantCommandHandlerTests()
    {
        _sqliteConnection = new SqliteConnection("Filename=:memory:");
        _sqliteConnection.Open();

        var options = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseSqlite(_sqliteConnection)
            .Options;

        _dbContext = new TenancyDbContext(options);
        _dbContext.Database.EnsureCreated();

        _jwtService = new TestJwtService("fake_jwt_token");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _sqliteConnection.Close();
        _sqliteConnection.Dispose();
    }

    [Fact]
    public async Task Handle_Should_Fail_When_Cnpj_Already_Exists()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Carlos Produtor",
            Email = "carlos@fazenda.com.br",
            PasswordHash = "hash"
        };
        _dbContext.Users.Add(user);
        _dbContext.Tenants.Add(new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Existing Farm",
            CNPJ = "12.345.678/0001-90",
            CnpjNormalized = "12345678000190",
            State = "MT",
            City = "Sinop",
            Status = "Active",
            SubscribedPlan = "Starter",
            Capacity = 500,
            StateRegistration = "IE",
            Type = "Corte",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var handler = new CreateTenantCommandHandler(_dbContext, _jwtService, new NoOpTenantDatabaseProvisioner());
        var command = new CreateTenantCommand(
            user.Id,
            "Fazenda Duplicada",
            "12.345.678/0001-90",
            "MT",
            "Sinop",
            "12345678",
            1200,
            "Pro",
            5000
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.CnpjAlreadyExists");
    }

    [Fact]
    public async Task Handle_Should_Create_Tenant_And_UserTenant_With_Default_Trial_Plan_And_Capacity()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Carlos Produtor",
            Email = "carlos@fazenda.com.br",
            PasswordHash = "hash"
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new CreateTenantCommandHandler(_dbContext, _jwtService, new NoOpTenantDatabaseProvisioner());
        var command = new CreateTenantCommand(
            user.Id,
            "Fazenda Esperança",
            "12.345.678/0001-90",
            "MT",
            "Sinop",
            "12345678",
            1200,
            "Starter",
            500
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Token.Should().Be("fake_jwt_token");
        result.Value.UserId.Should().Be(user.Id);

        var tenantInDb = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Name == "Fazenda Esperança");
        tenantInDb.Should().NotBeNull();
        tenantInDb!.State.Should().Be("MT");
        tenantInDb.SubscribedPlan.Should().Be("Starter");
        tenantInDb.Capacity.Should().Be(500);
        tenantInDb.Status.Should().Be("Trial");
        tenantInDb.CurrentPeriodEndUtc.Should().NotBeNull();
        tenantInDb.CurrentPeriodEndUtc.Should().BeCloseTo(DateTime.UtcNow.AddDays(CreateTenantCommand.DefaultTrialDays), TimeSpan.FromSeconds(30));
        CreateTenantCommand.DefaultTrialDays.Should().Be(30);

        var userTenantInDb = await _dbContext.UserTenants
            .FirstOrDefaultAsync(ut => ut.UserId == user.Id && ut.TenantId == tenantInDb.Id);
        userTenantInDb.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_Should_Enforce_Starter_Plan_And_Cap_Capacity_When_Payload_Has_Arbitrary_Values()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Roberto Bypasser",
            Email = "roberto@fazenda.com.br",
            PasswordHash = "hash"
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new CreateTenantCommandHandler(_dbContext, _jwtService, new NoOpTenantDatabaseProvisioner());
        var command = new CreateTenantCommand(
            user.Id,
            "Fazenda Ilimitada Forjada",
            "12.345.678/0001-90",
            "GO",
            "Jataí",
            "12345678",
            5000,
            "Enterprise",
            999999
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var tenantInDb = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Name == "Fazenda Ilimitada Forjada");
        tenantInDb.Should().NotBeNull();
        tenantInDb!.SubscribedPlan.Should().Be("Starter");
        tenantInDb.Capacity.Should().Be(500);
        tenantInDb.Status.Should().Be("Trial");
    }

    [Fact]
    public async Task Handle_Should_Respect_Valid_Custom_Capacity_Within_Starter_Limit()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Joao Pequeno Produtor",
            Email = "joao@pequena.com.br",
            PasswordHash = "hash"
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new CreateTenantCommandHandler(_dbContext, _jwtService, new NoOpTenantDatabaseProvisioner());
        var command = new CreateTenantCommand(
            user.Id,
            "Fazenda Pequena",
            "12.345.678/0001-90",
            "MS",
            "Bonito",
            "12345678",
            200,
            "Starter",
            250
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var tenantInDb = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Name == "Fazenda Pequena");
        tenantInDb.Should().NotBeNull();
        tenantInDb!.SubscribedPlan.Should().Be("Starter");
        tenantInDb.Capacity.Should().Be(250);
        tenantInDb.Status.Should().Be("Trial");
    }

    [Fact]
    public async Task Handle_Should_Fail_When_User_Does_Not_Exist()
    {
        // Arrange
        var handler = new CreateTenantCommandHandler(_dbContext, _jwtService, new NoOpTenantDatabaseProvisioner());
        var command = new CreateTenantCommand(
            Guid.NewGuid(),
            "Fazenda Inexistente",
            "00.000.000/0001-00",
            "GO",
            "Rio Verde",
            "IE123",
            500,
            "Starter",
            1000
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("User.NotFound");
    }

    [Fact]
    public async Task Handle_Should_Publish_TenantRegisteredIntegrationEvent_When_Tenant_Is_Created()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Produtor Indicado",
            Email = "indicado@fazenda.com.br",
            PasswordHash = "hash"
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var publisher = NSubstitute.Substitute.For<MediatR.IPublisher>();
        var handler = new CreateTenantCommandHandler(_dbContext, _jwtService, new NoOpTenantDatabaseProvisioner(), publisher);

        var command = new CreateTenantCommand(
            user.Id,
            "Fazenda Nova Era",
            "12.345.678/0001-90",
            "MS",
            "Bonito",
            "12345678",
            200,
            "Starter",
            500,
            ReferralCode: "AMIGO2026",
            OriginIpAddress: "200.150.100.50",
            UserAgent: "Mozilla/5.0 Chrome"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await publisher.Received(1).Publish(
            NSubstitute.Arg.Is<CriaCerto.Modules.Tenancy.Application.Events.TenantRegisteredIntegrationEvent>(e =>
                e.ReferralCode == "AMIGO2026" &&
                e.UserEmail == "indicado@fazenda.com.br" &&
                e.OriginIpAddress == "200.150.100.50" &&
                e.UserAgent == "Mozilla/5.0 Chrome" &&
                e.TenantId != Guid.Empty),
            NSubstitute.Arg.Any<CancellationToken>()
        );
    }

    private sealed class TestJwtService : IJwtService
    {
        private readonly string _token;
        public TestJwtService(string token) => _token = token;
        public string GenerateToken(User user, Tenant tenant, UserRole role = UserRole.Admin) => _token;
        public string GeneratePartnerToken(User user) => _token;
    }

    private sealed class NoOpTenantDatabaseProvisioner : CriaCerto.BuildingBlocks.Abstractions.Tenancy.ITenantDatabaseProvisioner
    {
        public Task EnsureTenantDatabaseAsync(Guid tenantId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}

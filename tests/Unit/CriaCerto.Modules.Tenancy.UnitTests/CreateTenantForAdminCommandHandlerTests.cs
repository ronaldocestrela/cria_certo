using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.BuildingBlocks.Abstractions.Tenancy;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Features.BackofficeTenants;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class CreateTenantForAdminCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TenancyDbContext _dbContext;

    public CreateTenantForAdminCommandHandlerTests()
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
    public async Task Handle_Should_Create_Tenant_And_Provision_Database()
    {
        var handler = new CreateTenantForAdminCommandHandler(_dbContext, new NoOpProvisioner());
        var command = new CreateTenantForAdminCommand(
            "Fazenda Admin", "Razão LTDA", "12.345.678/0001-90", "EXT-001",
            "MT", "Sinop", "12345678", 1000, "Starter", 500, "Corte",
            "João Técnico", "tecnico@fazenda.com", "Maria Comercial", "comercial@fazenda.com", null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ExternalIdentifier.Should().Be("EXT-001");
        result.Value.TechnicalOwnerName.Should().Be("João Técnico");

        var tenant = await _dbContext.Tenants.FirstAsync();
        tenant.CnpjNormalized.Should().Be("12345678000190");
    }

    [Fact]
    public async Task Handle_Should_Fail_When_Cnpj_Already_Exists()
    {
        _dbContext.Tenants.Add(new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Existing",
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

        var handler = new CreateTenantForAdminCommandHandler(_dbContext, new NoOpProvisioner());
        var result = await handler.Handle(new CreateTenantForAdminCommand(
            "Nova", null, "12.345.678/0001-90", null, "MT", "Sinop", "IE", 100, "Starter", 100, "Corte",
            null, null, null, null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.CnpjAlreadyExists");
    }

    [Fact]
    public async Task Handle_Should_Link_Owner_User_When_Email_Exists()
    {
        var user = new User { Id = Guid.NewGuid(), FullName = "Produtor", Email = "prod@faz.com", PasswordHash = "hash" };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new CreateTenantForAdminCommandHandler(_dbContext, new NoOpProvisioner());
        var result = await handler.Handle(new CreateTenantForAdminCommand(
            "Fazenda", null, "98.765.432/0001-10", null, "RS", "Porto Alegre", "IE", 500, "Starter", 200, "Corte",
            null, null, null, null, "prod@faz.com"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TeamMemberCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Should_Fail_When_Owner_User_Not_Found()
    {
        var handler = new CreateTenantForAdminCommandHandler(_dbContext, new NoOpProvisioner());
        var result = await handler.Handle(new CreateTenantForAdminCommand(
            "Fazenda", null, "98.765.432/0001-10", null, "RS", "Porto Alegre", "IE", 500, "Starter", 200, "Corte",
            null, null, null, null, "missing@faz.com"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("User.NotFound");
    }

    private sealed class NoOpProvisioner : ITenantDatabaseProvisioner
    {
        public Task EnsureTenantDatabaseAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

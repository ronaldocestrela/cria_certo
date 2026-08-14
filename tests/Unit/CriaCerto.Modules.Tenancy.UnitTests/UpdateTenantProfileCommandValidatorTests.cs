using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Features.UpdateTenantProfile;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class UpdateTenantProfileCommandValidatorTests : IDisposable
{
    private readonly SqliteConnection _sqliteConnection;
    private readonly TenancyDbContext _dbContext;
    private readonly UpdateTenantProfileCommandValidator _validator;

    public UpdateTenantProfileCommandValidatorTests()
    {
        _sqliteConnection = new SqliteConnection("Filename=:memory:");
        _sqliteConnection.Open();

        var options = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseSqlite(_sqliteConnection)
            .Options;

        _dbContext = new TenancyDbContext(options);
        _dbContext.Database.EnsureCreated();

        _validator = new UpdateTenantProfileCommandValidator(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _sqliteConnection.Close();
        _sqliteConnection.Dispose();
    }

    [Fact]
    public async Task Validate_Should_Fail_When_CNPJ_Is_Invalid()
    {
        // Arrange
        var command = new UpdateTenantProfileCommand(
            Guid.NewGuid(),
            "Fazenda Teste",
            "CNPJ_INVALIDO",
            "MT",
            "Sinop",
            "IE123",
            100,
            500,
            "Retiro"
        );

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CNPJ");
    }

    [Fact]
    public async Task Validate_Should_Fail_When_Capacity_Exceeds_Starter_Plan_Limit()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Pequena",
            CNPJ = "12.345.678/0001-99",
            CnpjNormalized = "12345678000199",
            SubscribedPlan = "Starter",
            Capacity = 500,
            State = "MT",
            City = "Sinop",
            Status = "Active",
            StateRegistration = "IE",
            Type = "Retiro",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var command = new UpdateTenantProfileCommand(
            tenant.Id,
            "Fazenda Pequena",
            "12.345.678/0001-99",
            "MT",
            "Sinop",
            "IE123",
            500,
            2500, // Exceeds Starter plan limit of 500
            "Retiro"
        );

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("excede o limite"));
    }

    [Fact]
    public async Task Validate_Should_Pass_When_Command_Is_Valid()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Pequena",
            CNPJ = "12.345.678/0001-99",
            CnpjNormalized = "12345678000199",
            SubscribedPlan = "Pro",
            Capacity = 2000,
            State = "MT",
            City = "Sinop",
            Status = "Active",
            StateRegistration = "IE",
            Type = "Retiro",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var command = new UpdateTenantProfileCommand(
            tenant.Id,
            "Fazenda Vista Verde",
            "12.345.678/0001-99",
            "MT",
            "Sorriso",
            "IE123456",
            1200.00m,
            2400, // Valid for Pro plan (limit 2500)
            "Recria e Engorda"
        );

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}

using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Features.Tenants.Commands;
using CriaCerto.Modules.Backoffice.Application.Features.Tenants.Dtos;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.UnitTests.Features;

public class SaveAdminFilterCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BackofficeDbContext _dbContext;

    public SaveAdminFilterCommandHandlerTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<BackofficeDbContext>().UseSqlite(_connection).Options;
        _dbContext = new BackofficeDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task Handle_Should_Fail_When_Name_Already_Exists_For_User()
    {
        var adminId = Guid.NewGuid();
        _dbContext.Set<AdminSavedFilter>().Add(AdminSavedFilter.Create(
            adminId,
            "High Churn MT",
            """{"ChurnRisk":"High","State":"MT"}""",
            false));
        await _dbContext.SaveChangesAsync();

        var handler = new SaveAdminFilterCommandHandler(_dbContext);
        var result = await handler.Handle(new SaveAdminFilterCommand(
            adminId,
            "High Churn MT",
            new TenantAdminFilterDto(ChurnRisk: "High", State: "MT"),
            false), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Backoffice.SavedFilterNameAlreadyExists");
    }

    [Fact]
    public async Task Handle_Should_Allow_Same_Name_For_Different_Users()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        _dbContext.Set<AdminSavedFilter>().Add(AdminSavedFilter.Create(
            userA,
            "Shared Name",
            """{"Status":"Active"}""",
            false));
        await _dbContext.SaveChangesAsync();

        var handler = new SaveAdminFilterCommandHandler(_dbContext);
        var result = await handler.Handle(new SaveAdminFilterCommand(
            userB,
            "Shared Name",
            new TenantAdminFilterDto(Status: "Active"),
            true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsDefault.Should().BeTrue();
    }
}

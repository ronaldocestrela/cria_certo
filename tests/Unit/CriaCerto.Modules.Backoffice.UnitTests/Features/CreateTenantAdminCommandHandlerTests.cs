using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Features.Tenants.Commands;
using CriaCerto.Modules.Backoffice.UnitTests.TestData;
using CriaCerto.Modules.Tenancy.Application.Contracts;
using CriaCerto.Modules.Tenancy.Application.Features.BackofficeTenants;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace CriaCerto.Modules.Backoffice.UnitTests.Features;

public class CreateTenantAdminCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BackofficeDbContext _dbContext;
    private readonly ISender _sender;

    public CreateTenantAdminCommandHandlerTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<BackofficeDbContext>().UseSqlite(_connection).Options;
        _dbContext = new BackofficeDbContext(options);
        _dbContext.Database.EnsureCreated();
        _sender = Substitute.For<ISender>();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task Handle_Should_Write_Audit_Log_On_Success()
    {
        var tenantId = Guid.NewGuid();
        _sender.Send(Arg.Any<CreateTenantForAdminCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(TenantBackofficeTestData.CreateDetail(tenantId)));

        var handler = new CreateTenantAdminCommandHandler(_sender, _dbContext);
        var result = await handler.Handle(new CreateTenantAdminCommand(
            "Fazenda", null, "12.345.678/0001-90", null, "MT", "Sinop", "IE", 1000, "Starter", 500, "Corte",
            null, null, null, null, null, Guid.NewGuid(), "admin@test.com", "127.0.0.1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var audit = await _dbContext.AuditLogs.FirstOrDefaultAsync();
        audit.Should().NotBeNull();
        audit!.Action.Should().Be("Tenant.Created");
        audit.Resource.Should().Be($"Tenant/{tenantId}");
    }

    [Fact]
    public async Task Handle_Should_Not_Write_Audit_When_Tenancy_Fails()
    {
        _sender.Send(Arg.Any<CreateTenantForAdminCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<TenantBackofficeDetailDto>(
                Error.Conflict("Tenant.CnpjAlreadyExists", "duplicate")));

        var handler = new CreateTenantAdminCommandHandler(_sender, _dbContext);
        var result = await handler.Handle(new CreateTenantAdminCommand(
            "Fazenda", null, "12.345.678/0001-90", null, "MT", "Sinop", "IE", 1000, "Starter", 500, "Corte",
            null, null, null, null, null, Guid.NewGuid(), "admin@test.com", "127.0.0.1"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        (await _dbContext.AuditLogs.CountAsync()).Should().Be(0);
    }
}

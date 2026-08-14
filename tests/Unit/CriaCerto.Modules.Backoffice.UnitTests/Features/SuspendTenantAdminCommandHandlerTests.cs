using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Features.Tenants.Commands;
using CriaCerto.Modules.Tenancy.Application.Contracts;
using CriaCerto.Modules.Tenancy.Application.Features.BackofficeTenants;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace CriaCerto.Modules.Backoffice.UnitTests.Features;

public class SuspendTenantAdminCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BackofficeDbContext _dbContext;
    private readonly ISender _sender;

    public SuspendTenantAdminCommandHandlerTests()
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
        var detail = CreateDetail(tenantId, "Active");

        _sender.Send(Arg.Any<GetTenantBackofficeDetailQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(detail));

        _sender.Send(Arg.Any<SuspendTenantForAdminCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(detail with { Status = "Suspended", StatusReason = "Inadimplência confirmada." }));

        var handler = new SuspendTenantAdminCommandHandler(_sender, _dbContext);
        var result = await handler.Handle(new SuspendTenantAdminCommand(
            tenantId,
            "Inadimplência confirmada.",
            Guid.NewGuid(),
            "admin@test.com",
            "127.0.0.1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var audit = await _dbContext.AuditLogs.FirstOrDefaultAsync();
        audit.Should().NotBeNull();
        audit!.Action.Should().Be("Tenant.Suspended");
        audit.Resource.Should().Be($"Tenant/{tenantId}");
    }

    [Fact]
    public async Task Handle_Should_Not_Write_Audit_When_Tenancy_Fails()
    {
        var tenantId = Guid.NewGuid();
        _sender.Send(Arg.Any<GetTenantBackofficeDetailQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(CreateDetail(tenantId, "Active")));

        _sender.Send(Arg.Any<SuspendTenantForAdminCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<TenantBackofficeDetailDto>(
                Error.Conflict("Tenant.ProtectedTenant", "protected")));

        var handler = new SuspendTenantAdminCommandHandler(_sender, _dbContext);
        var result = await handler.Handle(new SuspendTenantAdminCommand(
            tenantId,
            "Inadimplência confirmada.",
            Guid.NewGuid(),
            "admin@test.com",
            "127.0.0.1"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        (await _dbContext.AuditLogs.CountAsync()).Should().Be(0);
    }

    private static TenantBackofficeDetailDto CreateDetail(Guid tenantId, string status) =>
        new(
            tenantId, "Fazenda", null, "12.345.678/0001-90", null, status, "Starter",
            500, 500, false, "MT", "Sinop", "IE", 1000, "Corte",
            null, null, null, null, false, null, null, 0, 0, DateTime.UtcNow, DateTime.UtcNow);
}

using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Features.Support.Commands;
using CriaCerto.Modules.Backoffice.Application.Features.Support.Dtos;
using CriaCerto.Modules.Backoffice.Application.Features.Support.Queries;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using CriaCerto.Modules.Backoffice.UnitTests.TestData;
using CriaCerto.Modules.Tenancy.Application.Contracts;
using CriaCerto.Modules.Tenancy.Application.Features.BackofficeTenants;
using FluentAssertions;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Features;

public class SupportFeaturesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BackofficeDbContext _dbContext;
    private readonly ISender _sender;

    public SupportFeaturesTests()
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
    public async Task GetTenantDiagnostics_WhenTenantDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _sender.Send(Arg.Any<GetTenantBackofficeDetailQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<TenantBackofficeDetailDto>(Error.NotFound("Tenants.NotFound", "Tenant not found")));

        var handler = new GetTenantDiagnosticsQueryHandler(_sender, _dbContext);

        // Act
        var result = await handler.Handle(new GetTenantDiagnosticsQuery(tenantId), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(SupportErrors.TenantNotFound.Code);
    }

    [Fact]
    public async Task GetTenantDiagnostics_WhenTenantExists_ShouldReturnFullDiagnosticReport()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenantDetail = TenantBackofficeTestData.CreateDetail(tenantId, status: "Active");
        _sender.Send(Arg.Any<GetTenantBackofficeDetailQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(tenantDetail));

        var handler = new GetTenantDiagnosticsQueryHandler(_sender, _dbContext);

        // Act
        var result = await handler.Handle(new GetTenantDiagnosticsQuery(tenantId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Overview.Id.Should().Be(tenantId);
        result.Value.Overview.Status.Should().Be("Active");
        result.Value.SyncHealth.Should().NotBeNull();
        result.Value.Modules.Should().NotBeEmpty();
        result.Value.QueueHealth.Should().NotBeNull();
    }

    [Fact]
    public async Task GetSupportPlaybooks_ShouldReturnStandardOperationalPlaybooks()
    {
        // Arrange
        var handler = new GetSupportPlaybooksQueryHandler();

        // Act
        var result = await handler.Handle(new GetSupportPlaybooksQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        result.Value.Should().Contain(p => p.Code == "PB-SYNC-01");
        result.Value.Should().Contain(p => p.Code == "PB-ENT-02");
    }

    [Theory]
    [InlineData("", "Justificativa operacional válida com mais de 10 caracteres.")]
    [InlineData("AB", "Justificativa operacional válida com mais de 10 caracteres.")]
    public async Task ExecuteRemediation_WhenTicketIsInvalid_ShouldReturnValidationFailure(string invalidTicket, string justification)
    {
        // Arrange
        var handler = new ExecuteTenantRemediationCommandHandler(_sender, _dbContext);
        var command = new ExecuteTenantRemediationCommand(
            Guid.NewGuid(),
            "RequestClientCacheReset",
            invalidTicket,
            justification,
            Guid.NewGuid(),
            "support@criacerto.com.br",
            "127.0.0.1");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(SupportErrors.InvalidTicketId.Code);
    }

    [Theory]
    [InlineData("Curta")]
    [InlineData("")]
    public async Task ExecuteRemediation_WhenJustificationIsTooShort_ShouldReturnValidationFailure(string shortJustification)
    {
        // Arrange
        var handler = new ExecuteTenantRemediationCommandHandler(_sender, _dbContext);
        var command = new ExecuteTenantRemediationCommand(
            Guid.NewGuid(),
            "RequestClientCacheReset",
            "SUP-1010",
            shortJustification,
            Guid.NewGuid(),
            "support@criacerto.com.br",
            "127.0.0.1");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(SupportErrors.InvalidJustification.Code);
    }

    [Fact]
    public async Task ExecuteRemediation_WhenActionTypeIsUnknown_ShouldReturnValidationFailure()
    {
        // Arrange
        var handler = new ExecuteTenantRemediationCommandHandler(_sender, _dbContext);
        var command = new ExecuteTenantRemediationCommand(
            Guid.NewGuid(),
            "UnknownDangerousAction",
            "SUP-1010",
            "Justificativa operacional válida de suporte.",
            Guid.NewGuid(),
            "support@criacerto.com.br",
            "127.0.0.1");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(SupportErrors.InvalidActionType.Code);
    }

    [Fact]
    public async Task ExecuteRemediation_WhenValidRequest_ShouldExecuteAndRecordAuditLog()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var adminEmail = "support.n2@criacerto.com.br";
        var tenantDetail = TenantBackofficeTestData.CreateDetail(tenantId);

        _sender.Send(Arg.Any<GetTenantBackofficeDetailQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(tenantDetail));

        var handler = new ExecuteTenantRemediationCommandHandler(_sender, _dbContext);
        var command = new ExecuteTenantRemediationCommand(
            tenantId,
            "RequestClientCacheReset",
            "SUP-2024",
            "Dispositivo de campo relatou inconsistência no curral.",
            adminId,
            adminEmail,
            "192.168.1.100");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ActionType.Should().Be("RequestClientCacheReset");
        result.Value.SupportTicketId.Should().Be("SUP-2024");
        result.Value.OperatorEmail.Should().Be(adminEmail);

        // Check AuditLog was saved
        var auditLogs = await _dbContext.Set<AuditLog>().ToListAsync();
        auditLogs.Should().ContainSingle(a =>
            a.Action == "Support.RemediationExecuted" &&
            a.Resource == $"Tenant/{tenantId}" &&
            a.AdminUserId == adminId);
    }
}

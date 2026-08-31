using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Features.Impersonation.Commands;
using CriaCerto.Modules.Backoffice.Application.Security;
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

public class StartImpersonationSessionCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BackofficeDbContext _dbContext;
    private readonly ISender _sender;
    private readonly IBackofficeTokenService _tokenService;

    public StartImpersonationSessionCommandTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<BackofficeDbContext>().UseSqlite(_connection).Options;
        _dbContext = new BackofficeDbContext(options);
        _dbContext.Database.EnsureCreated();

        _sender = Substitute.For<ISender>();
        _tokenService = Substitute.For<IBackofficeTokenService>();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Handle_WhenTicketIsEmpty_ShouldReturnValidationFailure(string? invalidTicket)
    {
        // Arrange
        var handler = new StartImpersonationSessionCommandHandler(_sender, _dbContext, _tokenService);
        var command = new StartImpersonationSessionCommand(
            Guid.NewGuid(), null, invalidTicket!, "Justificativa válida com mais de 10 caracteres.", 15,
            Guid.NewGuid(), "admin@criacerto.com.br", "127.0.0.1", "Agent");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Impersonation.TicketRequired");
    }

    [Theory]
    [InlineData("a")]
    [InlineData("ab")]
    [InlineData("ticket com espacos")]
    [InlineData("ticket@!$%")]
    public async Task Handle_WhenTicketFormatIsInvalid_ShouldReturnValidationFailure(string invalidTicket)
    {
        // Arrange
        var handler = new StartImpersonationSessionCommandHandler(_sender, _dbContext, _tokenService);
        var command = new StartImpersonationSessionCommand(
            Guid.NewGuid(), null, invalidTicket, "Justificativa válida com mais de 10 caracteres.", 15,
            Guid.NewGuid(), "admin@criacerto.com.br", "127.0.0.1", "Agent");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Impersonation.InvalidTicketFormat");
    }

    [Theory]
    [InlineData("")]
    [InlineData("curta")]
    [InlineData("123456789")]
    public async Task Handle_WhenJustificationIsTooShort_ShouldReturnValidationFailure(string shortJustification)
    {
        // Arrange
        var handler = new StartImpersonationSessionCommandHandler(_sender, _dbContext, _tokenService);
        var command = new StartImpersonationSessionCommand(
            Guid.NewGuid(), null, "SUP-1044", shortJustification, 15,
            Guid.NewGuid(), "admin@criacerto.com.br", "127.0.0.1", "Agent");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Impersonation.JustificationRequired");
    }

    [Fact]
    public async Task Handle_WhenTenantNotFound_ShouldReturnNotFoundFailure()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _sender.Send(Arg.Any<GetTenantBackofficeDetailQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<TenantBackofficeDetailDto>(Error.NotFound("Tenant.NotFound", "Tenant não encontrado.")));

        var handler = new StartImpersonationSessionCommandHandler(_sender, _dbContext, _tokenService);
        var command = new StartImpersonationSessionCommand(
            tenantId, null, "SUP-1044", "Justificativa válida com mais de 10 caracteres.", 15,
            Guid.NewGuid(), "admin@criacerto.com.br", "127.0.0.1", "Agent");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Impersonation.TenantNotFound");
    }

    [Theory]
    [InlineData("Suspended")]
    [InlineData("Cancelled")]
    [InlineData("Archived")]
    public async Task Handle_WhenTenantIsBlocked_ShouldReturnConflictFailure(string blockedStatus)
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var detail = TenantBackofficeTestData.CreateDetail(tenantId, blockedStatus);
        _sender.Send(Arg.Any<GetTenantBackofficeDetailQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(detail));

        var handler = new StartImpersonationSessionCommandHandler(_sender, _dbContext, _tokenService);
        var command = new StartImpersonationSessionCommand(
            tenantId, null, "SUP-1044", "Justificativa válida com mais de 10 caracteres.", 15,
            Guid.NewGuid(), "admin@criacerto.com.br", "127.0.0.1", "Agent");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Impersonation.TenantBlocked");
    }

    [Fact]
    public async Task Handle_WhenTenantIsProtected_ShouldReturnConflictFailure()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var detail = TenantBackofficeTestData.CreateDetail(tenantId, "Active") with { IsProtected = true };
        _sender.Send(Arg.Any<GetTenantBackofficeDetailQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(detail));

        var handler = new StartImpersonationSessionCommandHandler(_sender, _dbContext, _tokenService);
        var command = new StartImpersonationSessionCommand(
            tenantId, null, "SUP-1044", "Justificativa válida com mais de 10 caracteres.", 15,
            Guid.NewGuid(), "admin@criacerto.com.br", "127.0.0.1", "Agent");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Impersonation.TenantProtected");
    }

    [Fact]
    public async Task Handle_WhenValid_ShouldPersistSession_GenerateToken_AndWriteAuditLog()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var adminEmail = "support.n2@criacerto.com.br";
        var expectedToken = "jwt_impersonation_token_xyz";

        var detail = TenantBackofficeTestData.CreateDetail(tenantId, "Active");
        _sender.Send(Arg.Any<GetTenantBackofficeDetailQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(detail));

        _tokenService.GenerateImpersonationToken(
            adminUserId,
            adminEmail,
            tenantId,
            detail.Name,
            Arg.Any<Guid?>(),
            Arg.Any<string?>(),
            Arg.Any<Guid>(),
            "SUP-2024",
            TimeSpan.FromMinutes(15))
            .Returns(expectedToken);

        var handler = new StartImpersonationSessionCommandHandler(_sender, _dbContext, _tokenService);
        var command = new StartImpersonationSessionCommand(
            tenantId, null, "SUP-2024", "Investigação técnica de divergência cadastral no módulo reprodutivo.", 15,
            adminUserId, adminEmail, "200.100.50.25", "Mozilla/5.0");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Token.Should().Be(expectedToken);
        result.Value.SupportTicket.Should().Be("SUP-2024");
        result.Value.TargetTenantId.Should().Be(tenantId);
        result.Value.Status.Should().Be("Active");
        result.Value.RemainingSeconds.Should().BeGreaterThan(0);

        // Check DB persisted session
        var session = await _dbContext.ImpersonationSessions.FirstOrDefaultAsync(s => s.Id == result.Value.SessionId);
        session.Should().NotBeNull();
        session!.SupportTicket.Should().Be("SUP-2024");
        session.Status.Should().Be(ImpersonationSessionStatus.Active);

        // Check DB AuditLog
        var audit = await _dbContext.AuditLogs.FirstOrDefaultAsync(a => a.Action == "Impersonation.Started");
        audit.Should().NotBeNull();
        audit!.AdminUserId.Should().Be(adminUserId);
        audit.Resource.Should().Be($"Tenant/{tenantId}");
        audit.DetailsJson.Should().Contain("SUP-2024");
    }

    [Fact]
    public async Task Handle_WhenPreviousSessionActive_ShouldRevokePreviousAndStartNew()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var adminEmail = "support.n2@criacerto.com.br";

        var oldSession = ImpersonationSession.Create(
            adminUserId, adminEmail, tenantId, "Old Tenant", null, null, "SUP-0001", "Old session reason", 15, "127.0.0.1", "Agent");
        _dbContext.ImpersonationSessions.Add(oldSession);
        await _dbContext.SaveChangesAsync();

        var detail = TenantBackofficeTestData.CreateDetail(tenantId, "Active");
        _sender.Send(Arg.Any<GetTenantBackofficeDetailQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(detail));

        _tokenService.GenerateImpersonationToken(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>(),
            Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<TimeSpan>())
            .Returns("token_new");

        var handler = new StartImpersonationSessionCommandHandler(_sender, _dbContext, _tokenService);
        var command = new StartImpersonationSessionCommand(
            tenantId, null, "SUP-2025", "Nova sessão de suporte para conferência de dados.", 20,
            adminUserId, adminEmail, "127.0.0.1", "Agent");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var reloadedOld = await _dbContext.ImpersonationSessions.FirstAsync(s => s.Id == oldSession.Id);
        reloadedOld.Status.Should().Be(ImpersonationSessionStatus.Revoked);
        reloadedOld.RevocationReason.Should().Contain("Substituída");
    }
}

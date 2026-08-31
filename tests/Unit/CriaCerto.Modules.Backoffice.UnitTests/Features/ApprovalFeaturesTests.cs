using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Features.Approvals.Commands;
using CriaCerto.Modules.Backoffice.Application.Features.Approvals.Queries;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Features;

public class ApprovalFeaturesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BackofficeDbContext _dbContext;
    private readonly ISender _sender;
    private readonly Guid _requesterId = Guid.NewGuid();
    private const string RequesterEmail = "requester@criacerto.com.br";
    private readonly Guid _reviewerId = Guid.NewGuid();
    private const string ReviewerEmail = "reviewer@criacerto.com.br";

    public ApprovalFeaturesTests()
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
    public async Task CreateApprovalRequest_WithValidData_ShouldPersistAndLogAudit()
    {
        // Arrange
        var handler = new CreateApprovalRequestCommandHandler(_dbContext);
        var command = new CreateApprovalRequestCommand(
            ApprovalRequestType.PublishPlanVersion,
            "Publicação de Plano",
            "Justificativa técnica detalhada para publicação.",
            "PlanVersion/10",
            "Impacto direto nas novas assinaturas",
            "{\"VersionId\":\"10\"}",
            _requesterId,
            RequesterEmail,
            "127.0.0.1",
            "SUP-2020");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(ApprovalRequestStatus.Pending);

        var saved = await _dbContext.AdminApprovalRequests.FirstOrDefaultAsync(r => r.Id == result.Value.Id);
        saved.Should().NotBeNull();
        saved!.Title.Should().Be("Publicação de Plano");

        var audit = await _dbContext.AuditLogs.FirstOrDefaultAsync(a => a.Action == "Approval.Requested");
        audit.Should().NotBeNull();
        audit!.AdminUserId.Should().Be(_requesterId);
    }

    [Fact]
    public async Task ApproveApprovalRequest_WhenReviewerIsRequester_ShouldFailWithCannotSelfApprove()
    {
        // Arrange
        var request = AdminApprovalRequest.Create(
            ApprovalRequestType.PublishPlanVersion,
            "Publicação de Plano",
            "Justificativa técnica detalhada para publicação.",
            "PlanVersion/10",
            "Impacto",
            "{\"VersionId\":\"10\"}",
            _requesterId,
            RequesterEmail).Value;

        _dbContext.AdminApprovalRequests.Add(request);
        await _dbContext.SaveChangesAsync();

        var handler = new ApproveApprovalRequestCommandHandler(_dbContext, _sender);
        var command = new ApproveApprovalRequestCommand(
            request.Id,
            _requesterId, // Same as requester! 4-eyes violation!
            RequesterEmail,
            "127.0.0.1",
            "Tentando autoaprovar");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ApprovalErrors.CannotSelfApprove.Code);

        var unaffected = await _dbContext.AdminApprovalRequests.FindAsync(request.Id);
        unaffected!.Status.Should().Be(ApprovalRequestStatus.Pending);
    }

    [Fact]
    public async Task ApproveApprovalRequest_WhenReviewerIsDifferent_AndPlanVersionExists_ShouldPublishAndExecute()
    {
        // Arrange: seed PlanCatalog with a Draft version
        var planCatalog = PlanCatalog.Create("STARTER", "Plano Starter", "Desc", "Cattle").Value;
        var versionResult = planCatalog.CreateVersion("v1.0", 99m, 990m, 500);
        var version = planCatalog.Versions.First();
        _dbContext.PlanCatalogs.Add(planCatalog);

        var request = AdminApprovalRequest.Create(
            ApprovalRequestType.PublishPlanVersion,
            "Publicação do Starter v1.0",
            "Justificativa técnica detalhada para publicação do catálogo.",
            $"PlanVersion/{version.Id}",
            "Impacta novos clientes",
            $"{{\"VersionId\":\"{version.Id}\",\"ApprovalNotes\":\"Aprovado pelo comitê\"}}",
            _requesterId,
            RequesterEmail).Value;

        _dbContext.AdminApprovalRequests.Add(request);
        await _dbContext.SaveChangesAsync();

        var handler = new ApproveApprovalRequestCommandHandler(_dbContext, _sender);
        var command = new ApproveApprovalRequestCommand(
            request.Id,
            _reviewerId,
            ReviewerEmail,
            "127.0.0.1",
            "Revisado e validado conforme proposta");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(ApprovalRequestStatus.Executed);

        var updatedRequest = await _dbContext.AdminApprovalRequests.FindAsync(request.Id);
        updatedRequest!.Status.Should().Be(ApprovalRequestStatus.Executed);
        updatedRequest.ReviewedByAdminUserId.Should().Be(_reviewerId);

        var updatedPlan = await _dbContext.PlanCatalogs.Include(p => p.Versions).FirstAsync(p => p.Id == planCatalog.Id);
        var publishedVersion = updatedPlan.Versions.First(v => v.Id == version.Id);
        publishedVersion.Status.Should().Be(PlanVersionStatus.Published);

        var audit = await _dbContext.AuditLogs.FirstOrDefaultAsync(a => a.Action == "Approval.ApprovedAndExecuted");
        audit.Should().NotBeNull();
        audit!.AdminUserId.Should().Be(_reviewerId);
    }

    [Fact]
    public async Task RejectApprovalRequest_WhenReviewerIsDifferent_ShouldRejectAndAudit()
    {
        // Arrange
        var request = AdminApprovalRequest.Create(
            ApprovalRequestType.PublishPlanVersion,
            "Publicação de Plano",
            "Justificativa técnica detalhada para publicação.",
            "PlanVersion/10",
            "Impacto",
            "{\"VersionId\":\"10\"}",
            _requesterId,
            RequesterEmail).Value;

        _dbContext.AdminApprovalRequests.Add(request);
        await _dbContext.SaveChangesAsync();

        var handler = new RejectApprovalRequestCommandHandler(_dbContext);
        var command = new RejectApprovalRequestCommand(
            request.Id,
            "Documentação financeira incompleta",
            _reviewerId,
            ReviewerEmail,
            "127.0.0.1");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(ApprovalRequestStatus.Rejected);

        var audit = await _dbContext.AuditLogs.FirstOrDefaultAsync(a => a.Action == "Approval.Rejected");
        audit.Should().NotBeNull();
        audit!.AdminUserId.Should().Be(_reviewerId);
    }

    [Fact]
    public async Task CancelApprovalRequest_WhenRequestedByRequester_ShouldCancel()
    {
        // Arrange
        var request = AdminApprovalRequest.Create(
            ApprovalRequestType.PublishPlanVersion,
            "Publicação de Plano",
            "Justificativa técnica detalhada para publicação.",
            "PlanVersion/10",
            "Impacto",
            "{\"VersionId\":\"10\"}",
            _requesterId,
            RequesterEmail).Value;

        _dbContext.AdminApprovalRequests.Add(request);
        await _dbContext.SaveChangesAsync();

        var handler = new CancelApprovalRequestCommandHandler(_dbContext);
        var command = new CancelApprovalRequestCommand(
            request.Id,
            _requesterId,
            RequesterEmail,
            "127.0.0.1",
            "Não será mais necessário");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(ApprovalRequestStatus.Cancelled);
    }

    [Fact]
    public async Task GetApprovalRequestsQuery_ShouldFilterAndCalculateCounts()
    {
        // Arrange
        var req1 = AdminApprovalRequest.Create(
            ApprovalRequestType.PublishPlanVersion,
            "Req 1",
            "Justificativa válida do primeiro pedido.",
            "Res/1",
            "Impacto",
            "{}",
            _requesterId,
            RequesterEmail).Value;

        var req2 = AdminApprovalRequest.Create(
            ApprovalRequestType.MassTenantSuspension,
            "Req 2",
            "Justificativa válida do segundo pedido.",
            "Res/2",
            "Impacto",
            "{}",
            _reviewerId,
            ReviewerEmail).Value;

        _dbContext.AdminApprovalRequests.AddRange(req1, req2);
        await _dbContext.SaveChangesAsync();

        var handler = new GetApprovalRequestsQueryHandler(_dbContext);

        // Act: Query list
        var listResult = await handler.Handle(new GetApprovalRequestsQuery(Status: ApprovalRequestStatus.Pending), CancellationToken.None);

        // Act: Query count for _reviewerId
        var countResult = await handler.Handle(new GetPendingApprovalsCountQuery(_reviewerId), CancellationToken.None);

        // Assert
        listResult.IsSuccess.Should().BeTrue();
        listResult.Value.TotalCount.Should().Be(2);

        countResult.IsSuccess.Should().BeTrue();
        countResult.Value.TotalPending.Should().Be(2);
        countResult.Value.MyPendingRequests.Should().Be(1);
        countResult.Value.PendingReviewForMe.Should().Be(1);
    }
}

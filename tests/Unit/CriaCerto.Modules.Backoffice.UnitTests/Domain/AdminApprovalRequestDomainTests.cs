using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Domain;

public class AdminApprovalRequestDomainTests
{
    private readonly Guid _requesterId = Guid.NewGuid();
    private const string RequesterEmail = "requester@criacerto.com.br";
    private readonly Guid _reviewerId = Guid.NewGuid();
    private const string ReviewerEmail = "reviewer@criacerto.com.br";

    [Fact]
    public void Create_WithValidData_ShouldInitializePendingRequest()
    {
        // Act
        var result = AdminApprovalRequest.Create(
            ApprovalRequestType.PublishPlanVersion,
            "Publicação do Plano Enterprise v2",
            "Justificativa válida com mais de 10 caracteres.",
            "PlanVersion/123",
            "Impacta preços e novos limites",
            "{\"VersionId\":\"123\"}",
            _requesterId,
            RequesterEmail,
            "SUP-1042",
            "{\"price\":{\"old\":100,\"new\":150}}",
            48);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var request = result.Value;
        request.Status.Should().Be(ApprovalRequestStatus.Pending);
        request.RequestType.Should().Be(ApprovalRequestType.PublishPlanVersion);
        request.Title.Should().Be("Publicação do Plano Enterprise v2");
        request.RequestedByAdminUserId.Should().Be(_requesterId);
        request.RequestedByAdminEmail.Should().Be(RequesterEmail);
        request.SupportTicketId.Should().Be("SUP-1042");
        request.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow);
        request.ReviewedByAdminUserId.Should().BeNull();
        request.ExecutedAtUtc.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("curta")]
    public void Create_WhenJustificationIsTooShort_ShouldReturnFailure(string shortJustification)
    {
        // Act
        var result = AdminApprovalRequest.Create(
            ApprovalRequestType.PublishPlanVersion,
            "Título válido",
            shortJustification,
            "PlanVersion/123",
            "Impacto",
            "{}",
            _requesterId,
            RequesterEmail);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ApprovalErrors.JustificationRequired.Code);
    }

    [Fact]
    public void Approve_WhenReviewerIsTheRequester_ShouldViolate4EyesPrincipleAndFail()
    {
        // Arrange
        var request = AdminApprovalRequest.Create(
            ApprovalRequestType.PublishPlanVersion,
            "Publicação de Plano",
            "Justificativa com mais de 10 caracteres.",
            "PlanVersion/123",
            "Impacto",
            "{}",
            _requesterId,
            RequesterEmail).Value;

        // Act - requester tries to self-approve!
        var approveResult = request.Approve(_requesterId, RequesterEmail, "Tentativa de autoaprovação");

        // Assert
        approveResult.IsFailure.Should().BeTrue();
        approveResult.Error.Code.Should().Be(ApprovalErrors.CannotSelfApprove.Code);
        request.Status.Should().Be(ApprovalRequestStatus.Pending);
        request.ReviewedByAdminUserId.Should().BeNull();
    }

    [Fact]
    public void Approve_WhenReviewerIsDifferentAdmin_ShouldApproveSuccessfully()
    {
        // Arrange
        var request = AdminApprovalRequest.Create(
            ApprovalRequestType.PublishPlanVersion,
            "Publicação de Plano",
            "Justificativa com mais de 10 caracteres.",
            "PlanVersion/123",
            "Impacto",
            "{}",
            _requesterId,
            RequesterEmail).Value;

        // Act - different admin approves
        var approveResult = request.Approve(_reviewerId, ReviewerEmail, "Aprovado com base na ata #45");

        // Assert
        approveResult.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(ApprovalRequestStatus.Approved);
        request.ReviewedByAdminUserId.Should().Be(_reviewerId);
        request.ReviewedByAdminEmail.Should().Be(ReviewerEmail);
        request.ReviewedAtUtc.Should().NotBeNull();
        request.ReviewNotes.Should().Be("Aprovado com base na ata #45");
    }

    [Fact]
    public void Reject_WhenReviewerIsTheRequester_ShouldViolate4EyesPrincipleAndFail()
    {
        // Arrange
        var request = AdminApprovalRequest.Create(
            ApprovalRequestType.PublishPlanVersion,
            "Publicação de Plano",
            "Justificativa com mais de 10 caracteres.",
            "PlanVersion/123",
            "Impacto",
            "{}",
            _requesterId,
            RequesterEmail).Value;

        // Act - requester tries to reject
        var rejectResult = request.Reject(_requesterId, RequesterEmail, "Motivo válido de rejeição");

        // Assert
        rejectResult.IsFailure.Should().BeTrue();
        rejectResult.Error.Code.Should().Be(ApprovalErrors.CannotSelfApprove.Code);
        request.Status.Should().Be(ApprovalRequestStatus.Pending);
    }

    [Fact]
    public void Reject_WhenReviewerIsDifferentAdmin_ShouldRejectSuccessfully()
    {
        // Arrange
        var request = AdminApprovalRequest.Create(
            ApprovalRequestType.PublishPlanVersion,
            "Publicação de Plano",
            "Justificativa com mais de 10 caracteres.",
            "PlanVersion/123",
            "Impacto",
            "{}",
            _requesterId,
            RequesterEmail).Value;

        // Act
        var rejectResult = request.Reject(_reviewerId, ReviewerEmail, "Preço incompatível com mercado");

        // Assert
        rejectResult.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(ApprovalRequestStatus.Rejected);
        request.ReviewedByAdminUserId.Should().Be(_reviewerId);
        request.RejectionReason.Should().Be("Preço incompatível com mercado");
    }

    [Fact]
    public void Cancel_WhenCancelledByRequester_ShouldCancelSuccessfully()
    {
        // Arrange
        var request = AdminApprovalRequest.Create(
            ApprovalRequestType.PublishPlanVersion,
            "Publicação de Plano",
            "Justificativa com mais de 10 caracteres.",
            "PlanVersion/123",
            "Impacto",
            "{}",
            _requesterId,
            RequesterEmail).Value;

        // Act
        var cancelResult = request.Cancel(_requesterId, "Desistência da publicação");

        // Assert
        cancelResult.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(ApprovalRequestStatus.Cancelled);
    }

    [Fact]
    public void Cancel_WhenCancelledByNonRequester_ShouldFail()
    {
        // Arrange
        var request = AdminApprovalRequest.Create(
            ApprovalRequestType.PublishPlanVersion,
            "Publicação de Plano",
            "Justificativa com mais de 10 caracteres.",
            "PlanVersion/123",
            "Impacto",
            "{}",
            _requesterId,
            RequesterEmail).Value;

        // Act
        var cancelResult = request.Cancel(_reviewerId, "Não sou o solicitante");

        // Assert
        cancelResult.IsFailure.Should().BeTrue();
        cancelResult.Error.Code.Should().Be(ApprovalErrors.OnlyRequesterCanCancel.Code);
        request.Status.Should().Be(ApprovalRequestStatus.Pending);
    }

    [Fact]
    public void MarkAsExecuted_WhenApproved_ShouldSetExecutedStatus()
    {
        // Arrange
        var request = AdminApprovalRequest.Create(
            ApprovalRequestType.PublishPlanVersion,
            "Publicação de Plano",
            "Justificativa com mais de 10 caracteres.",
            "PlanVersion/123",
            "Impacto",
            "{}",
            _requesterId,
            RequesterEmail).Value;

        request.Approve(_reviewerId, ReviewerEmail, "Aprovado");

        // Act
        var execResult = request.MarkAsExecuted("{\"versionPublished\":true}");

        // Assert
        execResult.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(ApprovalRequestStatus.Executed);
        request.ExecutedAtUtc.Should().NotBeNull();
        request.ExecutionResultJson.Should().Be("{\"versionPublished\":true}");
    }

    [Fact]
    public void MarkAsExecuted_WhenPending_ShouldFail()
    {
        // Arrange
        var request = AdminApprovalRequest.Create(
            ApprovalRequestType.PublishPlanVersion,
            "Publicação de Plano",
            "Justificativa com mais de 10 caracteres.",
            "PlanVersion/123",
            "Impacto",
            "{}",
            _requesterId,
            RequesterEmail).Value;

        // Act
        var execResult = request.MarkAsExecuted();

        // Assert
        execResult.IsFailure.Should().BeTrue();
        execResult.Error.Code.Should().Be(ApprovalErrors.CannotExecuteUnapproved.Code);
    }
}

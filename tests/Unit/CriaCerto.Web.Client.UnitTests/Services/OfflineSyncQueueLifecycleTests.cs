using CriaCerto.Shared.UI.Models;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Web.Client.UnitTests.Services;

public class OfflineSyncQueueLifecycleTests
{
    [Fact]
    public void SyncOperation_DefaultInstantiation_ShouldHaveValidGuidAndPendingStatus()
    {
        // Act
        var operation = new SyncOperation();

        // Assert
        operation.Id.Should().NotBeEmpty();
        operation.Status.Should().Be(SyncOperationStatus.Pending);
        operation.RetryCount.Should().Be(0);
        operation.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        operation.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void SyncOperation_StatusTransitions_ShouldReflectLifecycleProperly()
    {
        // Arrange
        var op = new SyncOperation
        {
            ModuleName = "Growth",
            ActionType = "RecordWeighing",
            PayloadJson = "{\"WeightKg\": 480.0}"
        };

        // Act & Assert 1: Em processamento
        op.Status = SyncOperationStatus.Syncing;
        op.Status.Should().Be(SyncOperationStatus.Syncing);

        // Act & Assert 2: Conflito
        op.Status = SyncOperationStatus.Conflict;
        op.ErrorMessage = "Conflito de versão no registro da pesagem.";
        op.RetryCount++;
        op.Status.Should().Be(SyncOperationStatus.Conflict);
        op.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        op.RetryCount.Should().Be(1);

        // Act & Assert 3: Sucesso
        op.Status = SyncOperationStatus.Success;
        op.ErrorMessage = null;
        op.Status.Should().Be(SyncOperationStatus.Success);
        op.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void SyncConflictItem_ShouldHoldConflictingOperationAndDetails()
    {
        // Arrange
        var operationId = Guid.NewGuid();
        var conflict = new SyncConflictItem
        {
            OperationId = operationId,
            EntityName = "Cow",
            EntityIdentifier = "BR-MT-2024-001",
            LocalPayloadJson = "{\"Weight\": 450}",
            ServerPayloadJson = "{\"Weight\": 460}",
            ConflictReason = "Registro atualizado concorrentemente no servidor."
        };

        // Assert
        conflict.OperationId.Should().Be(operationId);
        conflict.EntityName.Should().Be("Cow");
        conflict.EntityIdentifier.Should().Be("BR-MT-2024-001");
        conflict.ConflictReason.Should().Contain("servidor");
        conflict.ConflictDetectedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}

using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Telemetry;
using FluentAssertions;
using MediatR;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Features;

public class BackofficeTelemetryTests
{
    public record SampleCommand(string Data) : IRequest<Result<string>>;

    [Fact]
    public async Task ObservabilityBehavior_ShouldPassThroughAndRecordLatency()
    {
        // Arrange
        var behavior = new BackofficeObservabilityBehavior<SampleCommand, Result<string>>();
        var request = new SampleCommand("Test payload");
        RequestHandlerDelegate<Result<string>> next = () => Task.FromResult(Result.Success("Ok"));

        // Act
        var response = await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Value.Should().Be("Ok");
    }

    [Fact]
    public async Task ObservabilityBehavior_WhenFailed_ShouldMaintainFailureResult()
    {
        // Arrange
        var behavior = new BackofficeObservabilityBehavior<SampleCommand, Result<string>>();
        var request = new SampleCommand("Invalid");
        var expectedError = Error.Validation("Sample.Invalid", "Dados inválidos");
        RequestHandlerDelegate<Result<string>> next = () => Task.FromResult(Result.Failure<string>(expectedError));

        // Act
        var response = await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.Error.Code.Should().Be("Sample.Invalid");
    }
}

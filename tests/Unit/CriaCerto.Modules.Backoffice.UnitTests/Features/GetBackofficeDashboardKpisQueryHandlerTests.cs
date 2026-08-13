using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Features.Dashboard;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Features;

public class GetBackofficeDashboardKpisQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenExecuted_ShouldReturnDashboardKpisWrappedInResultSuccess()
    {
        // Arrange
        var handler = new GetBackofficeDashboardKpisQueryHandler();
        var query = new GetBackofficeDashboardKpisQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.TotalTenants.Should().BeGreaterThanOrEqualTo(0);
        result.Value.ActiveTenants.Should().BeGreaterThanOrEqualTo(0);
        result.Value.SystemHealthStatus.Should().Be("Healthy");
    }
}

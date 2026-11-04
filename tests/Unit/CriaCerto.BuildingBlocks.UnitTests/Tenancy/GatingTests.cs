using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CriaCerto.BuildingBlocks.Abstractions.Licensing;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.BuildingBlocks.Abstractions.Tenancy;
using CriaCerto.BuildingBlocks.Application.Behaviors;
using FluentAssertions;
using MediatR;
using NSubstitute;
using Xunit;

namespace CriaCerto.BuildingBlocks.UnitTests.Tenancy;

public class GatingTests
{
    [RequiresModule("Nutrition")]
    public record RestrictedRequest : IRequest<Result>;

    [RequiresModule("Breeding")]
    public record BreedingRequest : IRequest<Result>;

    [RequiresModule("Cows")]
    public record CorePlantelRequest : IRequest<Result>;

    [RequiresModule("Nutrition")]
    public record RestrictedGenericRequest : IRequest<Result<string>>;

    [Fact]
    public async Task ModuleAccessBehavior_ShouldBlockUnsubscribedModule_WithModuleNotSubscribedError()
    {
        // Arrange: cliente com Cows + Sanitary, mas sem Nutrition
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.SubscribedModules.Returns(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Cows", "Sanitary" });

        var behavior = new ModuleAccessBehavior<RestrictedRequest, Result>(tenantContext);
        var request = new RestrictedRequest();

        bool nextCalled = false;
        RequestHandlerDelegate<Result> next = () =>
        {
            nextCalled = true;
            return Task.FromResult(Result.Success());
        };

        // Act
        var result = await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be("License.ModuleNotSubscribed");
        result.Error.Message.Should().Contain("Nutrition");
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task ModuleAccessBehavior_ShouldBlockBreeding_WhenOnlyCowsAndSanitaryAreSubscribed()
    {
        // Arrange: cliente com Cows + Sanitary, mas sem Breeding
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.SubscribedModules.Returns(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Cows", "Sanitary" });

        var behavior = new ModuleAccessBehavior<BreedingRequest, Result>(tenantContext);
        var request = new BreedingRequest();

        bool nextCalled = false;
        RequestHandlerDelegate<Result> next = () =>
        {
            nextCalled = true;
            return Task.FromResult(Result.Success());
        };

        // Act
        var result = await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("License.ModuleNotSubscribed");
        result.Error.Message.Should().Contain("Breeding");
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task ModuleAccessBehavior_ShouldAllowModule_WhenModuleIsInSubscribedModules()
    {
        // Arrange
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.SubscribedModules.Returns(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Cows", "Nutrition" });

        var behavior = new ModuleAccessBehavior<RestrictedRequest, Result>(tenantContext);
        var request = new RestrictedRequest();

        bool nextCalled = false;
        RequestHandlerDelegate<Result> next = () =>
        {
            nextCalled = true;
            return Task.FromResult(Result.Success());
        };

        // Act
        var result = await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ModuleAccessBehavior_ShouldAlwaysAllowCoreCowsModule()
    {
        // Arrange
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.SubscribedModules.Returns(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Cows" });

        var behavior = new ModuleAccessBehavior<CorePlantelRequest, Result>(tenantContext);
        var request = new CorePlantelRequest();

        bool nextCalled = false;
        RequestHandlerDelegate<Result> next = () =>
        {
            nextCalled = true;
            return Task.FromResult(Result.Success());
        };

        // Act
        var result = await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ModuleAccessBehavior_ShouldBlockUnsubscribedGenericModule_WithModuleNotSubscribedError()
    {
        // Arrange
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.SubscribedModules.Returns(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Cows", "Breeding" });

        var behavior = new ModuleAccessBehavior<RestrictedGenericRequest, Result<string>>(tenantContext);
        var request = new RestrictedGenericRequest();

        bool nextCalled = false;
        RequestHandlerDelegate<Result<string>> next = () =>
        {
            nextCalled = true;
            return Task.FromResult(Result.Success("Ok"));
        };

        // Act
        var result = await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be("License.ModuleNotSubscribed");
        result.Error.Message.Should().Contain("Nutrition");
        nextCalled.Should().BeFalse();
    }
}

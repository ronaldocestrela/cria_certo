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

    [RequiresModule("Calving")]
    public record CalvingRequest : IRequest<Result>;

    [RequiresModule("Growth")]
    public record GrowthRequest : IRequest<Result>;

    [RequiresModule("Sanitary")]
    public record SanitaryRequest : IRequest<Result>;

    [RequiresModule("Analytics")]
    public record AnalyticsRequest : IRequest<Result>;

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

    [Fact]
    public async Task ModuleAccessBehavior_ShouldAllowSanitary_WhenTenantHasCowsAndSanitary()
    {
        // Arrange
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.SubscribedModules.Returns(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Cows", "Sanitary" });

        var behavior = new ModuleAccessBehavior<SanitaryRequest, Result>(tenantContext);
        var request = new SanitaryRequest();

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

    [Theory]
    [InlineData("Calving")]
    [InlineData("Growth")]
    [InlineData("Analytics")]
    public async Task ModuleAccessBehavior_ShouldBlockUnsubscribedCommercialModules_WhenOnlyCowsAndSanitarySubscribed(string moduleKey)
    {
        // Arrange
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.SubscribedModules.Returns(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Cows", "Sanitary" });

        IRequest<Result> request = moduleKey switch
        {
            "Calving" => new CalvingRequest(),
            "Growth" => new GrowthRequest(),
            "Analytics" => new AnalyticsRequest(),
            _ => throw new ArgumentOutOfRangeException(nameof(moduleKey))
        };

        bool nextCalled = false;
        RequestHandlerDelegate<Result> next = () =>
        {
            nextCalled = true;
            return Task.FromResult(Result.Success());
        };

        Result result = moduleKey switch
        {
            "Calving" => await new ModuleAccessBehavior<CalvingRequest, Result>(tenantContext).Handle((CalvingRequest)request, next, CancellationToken.None),
            "Growth" => await new ModuleAccessBehavior<GrowthRequest, Result>(tenantContext).Handle((GrowthRequest)request, next, CancellationToken.None),
            "Analytics" => await new ModuleAccessBehavior<AnalyticsRequest, Result>(tenantContext).Handle((AnalyticsRequest)request, next, CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(moduleKey))
        };

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be("License.ModuleNotSubscribed");
        result.Error.Message.Should().Contain(moduleKey);
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task ModuleAccessBehavior_WhenOnlyCoreCowsSubscribed_ShouldBlockAllOtherCommercialModules()
    {
        // Arrange
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.SubscribedModules.Returns(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Cows" });

        RequestHandlerDelegate<Result> next = () => Task.FromResult(Result.Success());

        var breedingResult = await new ModuleAccessBehavior<BreedingRequest, Result>(tenantContext)
            .Handle(new BreedingRequest(), next, CancellationToken.None);
        var calvingResult = await new ModuleAccessBehavior<CalvingRequest, Result>(tenantContext)
            .Handle(new CalvingRequest(), next, CancellationToken.None);
        var growthResult = await new ModuleAccessBehavior<GrowthRequest, Result>(tenantContext)
            .Handle(new GrowthRequest(), next, CancellationToken.None);
        var sanitaryResult = await new ModuleAccessBehavior<SanitaryRequest, Result>(tenantContext)
            .Handle(new SanitaryRequest(), next, CancellationToken.None);
        var nutritionResult = await new ModuleAccessBehavior<RestrictedRequest, Result>(tenantContext)
            .Handle(new RestrictedRequest(), next, CancellationToken.None);
        var analyticsResult = await new ModuleAccessBehavior<AnalyticsRequest, Result>(tenantContext)
            .Handle(new AnalyticsRequest(), next, CancellationToken.None);

        breedingResult.IsFailure.Should().BeTrue();
        calvingResult.IsFailure.Should().BeTrue();
        growthResult.IsFailure.Should().BeTrue();
        sanitaryResult.IsFailure.Should().BeTrue();
        nutritionResult.IsFailure.Should().BeTrue();
        analyticsResult.IsFailure.Should().BeTrue();
    }
}

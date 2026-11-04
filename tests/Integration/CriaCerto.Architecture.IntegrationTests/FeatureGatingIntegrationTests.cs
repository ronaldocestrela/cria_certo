using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CriaCerto.BuildingBlocks.Abstractions.Licensing;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.BuildingBlocks.Abstractions.Tenancy;
using CriaCerto.BuildingBlocks.Application.Abstractions.Messaging;
using CriaCerto.BuildingBlocks.Application.Behaviors;
using FluentAssertions;
using MediatR;
using NSubstitute;
using Xunit;

namespace CriaCerto.Architecture.IntegrationTests;

[RequiresModule("Feedlot")]
public record TestLockedCommand : ICommand<string>;

[RequiresModule("Nutrition")]
public record TestNutritionCommand : ICommand<string>;

[RequiresModule("Cows")]
public record TestCoreCowsCommand : ICommand<string>;

[RequiresModule("Breeding")]
public record TestBreedingCommand : ICommand<string>;

public class FeatureGatingIntegrationTests
{
    [Fact]
    public async Task ModuleAccessBehavior_WhenModuleIsNotInSubscribedModules_ShouldReturnModuleNotSubscribedResult()
    {
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.SubscribedModules.Returns(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Cows", "Sanitary" });

        var behavior = new ModuleAccessBehavior<TestLockedCommand, Result<string>>(tenantContext);

        RequestHandlerDelegate<Result<string>> next = () => Task.FromResult(Result.Success("OK"));

        var result = await behavior.Handle(new TestLockedCommand(), next, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be("License.ModuleNotSubscribed");
        result.Error.Message.Should().Contain("Feedlot");
    }

    [Fact]
    public void ModuleLicenseChecker_HasModuleAccess_ShouldValidateSubscribedModulesCorrectly()
    {
        var subscribedModules = new[] { "Cows", "Calving", "Sanitary" };

        ModuleLicenseChecker.HasModuleAccess(subscribedModules, "Cows").Should().BeTrue();
        ModuleLicenseChecker.HasModuleAccess(subscribedModules, "Tenancy").Should().BeTrue();
        ModuleLicenseChecker.HasModuleAccess(subscribedModules, "Calving").Should().BeTrue();
        ModuleLicenseChecker.HasModuleAccess(subscribedModules, "Sanitary").Should().BeTrue();
        ModuleLicenseChecker.HasModuleAccess(subscribedModules, "Breeding").Should().BeFalse();
        ModuleLicenseChecker.HasModuleAccess(subscribedModules, "Feedlot").Should().BeFalse();
    }

    [Fact]
    public async Task ModuleAccessBehavior_WhenNutritionIsSubscribed_ShouldAllowNutritionCommand()
    {
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.SubscribedModules.Returns(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Cows", "Nutrition" });

        var behavior = new ModuleAccessBehavior<TestNutritionCommand, Result<string>>(tenantContext);

        RequestHandlerDelegate<Result<string>> next = () => Task.FromResult(Result.Success("OK"));

        var result = await behavior.Handle(new TestNutritionCommand(), next, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("OK");
    }

    [Fact]
    public async Task ModuleAccessBehavior_WhenTenantHasCowsAndSanitaryWithoutBreeding_ShouldAllowCowsAndBlockBreeding()
    {
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.SubscribedModules.Returns(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Cows", "Sanitary" });

        var cowsBehavior = new ModuleAccessBehavior<TestCoreCowsCommand, Result<string>>(tenantContext);
        var breedingBehavior = new ModuleAccessBehavior<TestBreedingCommand, Result<string>>(tenantContext);

        RequestHandlerDelegate<Result<string>> next = () => Task.FromResult(Result.Success("OK"));

        var cowsResult = await cowsBehavior.Handle(new TestCoreCowsCommand(), next, CancellationToken.None);
        var breedingResult = await breedingBehavior.Handle(new TestBreedingCommand(), next, CancellationToken.None);

        cowsResult.IsSuccess.Should().BeTrue();
        breedingResult.IsFailure.Should().BeTrue();
        breedingResult.Error.Code.Should().Be("License.ModuleNotSubscribed");
        breedingResult.Error.Message.Should().Contain("Breeding");
    }
}

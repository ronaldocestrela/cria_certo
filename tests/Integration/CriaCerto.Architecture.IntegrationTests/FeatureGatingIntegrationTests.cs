using CriaCerto.BuildingBlocks.Abstractions.Licensing;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.BuildingBlocks.Abstractions.Tenancy;
using CriaCerto.BuildingBlocks.Application.Behaviors;
using CriaCerto.BuildingBlocks.Application.Abstractions.Messaging;
using FluentAssertions;
using MediatR;
using NSubstitute;
using Xunit;

namespace CriaCerto.Architecture.IntegrationTests;

[RequiresModule("Feedlot")]
public record TestLockedCommand : ICommand<string>;

public class FeatureGatingIntegrationTests
{
    [Fact]
    public async Task ModuleAccessBehavior_WhenPlanDoesNotSupportModule_ShouldReturnUnauthorizedResult()
    {
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.SubscribedPlan.Returns("Starter"); // Starter não possui acesso ao Feedlot

        var behavior = new ModuleAccessBehavior<TestLockedCommand, Result<string>>(tenantContext);

        RequestHandlerDelegate<Result<string>> next = () => Task.FromResult(Result.Success("OK"));

        var result = await behavior.Handle(new TestLockedCommand(), next, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be("License.AccessDenied");
    }

    [Fact]
    public void ModuleLicenseChecker_StarterPlan_ShouldAllowBreedingAndCalving()
    {
        ModuleLicenseChecker.HasAccess("Starter", "Breeding").Should().BeTrue();
        ModuleLicenseChecker.HasAccess("Starter", "Calving").Should().BeTrue();
        ModuleLicenseChecker.HasAccess("Starter", "Feedlot").Should().BeFalse();
    }

    [Fact]
    public async Task ModuleAccessBehavior_WhenPlanIsCommercialNameProFazenda_ShouldAllowNutrition()
    {
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.SubscribedPlan.Returns("Pro Fazenda");

        var behavior = new ModuleAccessBehavior<TestNutritionCommand, Result<string>>(tenantContext);

        RequestHandlerDelegate<Result<string>> next = () => Task.FromResult(Result.Success("OK"));

        var result = await behavior.Handle(new TestNutritionCommand(), next, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("OK");
    }

    [Fact]
    public async Task ModuleAccessBehavior_WhenPlanIsCommercialNameEnterpriseConfinamento_ShouldAllowFeedlot()
    {
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.SubscribedPlan.Returns("Enterprise Confinamento");

        var behavior = new ModuleAccessBehavior<TestLockedCommand, Result<string>>(tenantContext);

        RequestHandlerDelegate<Result<string>> next = () => Task.FromResult(Result.Success("OK"));

        var result = await behavior.Handle(new TestLockedCommand(), next, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("OK");
    }
}

[RequiresModule("Nutrition")]
public record TestNutritionCommand : ICommand<string>;


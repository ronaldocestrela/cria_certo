using CriaCerto.Shared.UI.Abstractions;
using CriaCerto.Shared.UI.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;
using Xunit;

namespace CriaCerto.Web.Client.UnitTests.Services;

public class SharedServiceDependencyInjectionTests
{
    private readonly IServiceProvider _serviceProvider;

    public SharedServiceDependencyInjectionTests()
    {
        var services = new ServiceCollection();

        // Host and Platform registrations (matching WebAssembly runtime)
        services.AddSingleton(Substitute.For<IJSRuntime>());
        services.AddSingleton(Substitute.For<ITokenStorageService>());
        services.AddSingleton(Substitute.For<IFileExportService>());
        services.AddSingleton(Substitute.For<IConnectivityService>());
        services.AddSingleton(Substitute.For<IOfflineSyncStorage>());
        services.AddSingleton(new HttpClient());

        // Shared Services
        services.AddCriaCertoSharedServices();

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public void BackofficeApiClient_WhenBothTokenStorageAndJSRuntimeRegistered_ResolvesWithoutAmbiguity()
    {
        var actInterface = () => _serviceProvider.GetRequiredService<IBackofficeApiClient>();
        var actConcrete = () => _serviceProvider.GetRequiredService<BackofficeApiClient>();

        actInterface.Should().NotThrow<Exception>();
        actConcrete.Should().NotThrow<Exception>();

        var client = actInterface();
        client.Should().NotBeNull();
        client.Should().BeOfType<BackofficeApiClient>();
    }

    [Theory]
    [InlineData(typeof(PlantelApiClient))]
    [InlineData(typeof(BreedingOpsApiClient))]
    [InlineData(typeof(GrowthApiClient))]
    [InlineData(typeof(NutritionApiClient))]
    [InlineData(typeof(CalvingApiClient))]
    [InlineData(typeof(SanitaryApiClient))]
    [InlineData(typeof(AnalyticsApiClient))]
    [InlineData(typeof(TenancyApiClient))]
    [InlineData(typeof(IBackofficeApiClient))]
    [InlineData(typeof(IPartnerApiClient))]
    [InlineData(typeof(IReferralApiClient))]
    public void AllApiClients_WhenBothTokenStorageAndJSRuntimeRegistered_ResolveWithoutAmbiguity(Type serviceType)
    {
        var act = () => _serviceProvider.GetRequiredService(serviceType);

        act.Should().NotThrow<Exception>();
        var resolvedService = act();
        resolvedService.Should().NotBeNull();
    }
}


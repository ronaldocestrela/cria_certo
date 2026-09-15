using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using CriaCerto.Modules.Backoffice.Application.Security;
using CriaCerto.Web.Client.Models;
using CriaCerto.Web.Client.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CriaCerto.Web.Client.UnitTests.Services;

public class FeatureFlagClientServiceTests
{
    private readonly IBackofficePermissionService _permissionService;

    public FeatureFlagClientServiceTests()
    {
        _permissionService = Substitute.For<IBackofficePermissionService>();
    }

    private FeatureFlagClientService CreateService(List<FeatureFlagClientDto> flags)
    {
        var handler = new MockHttpMessageHandler(flags);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost:5001/")
        };

        return new FeatureFlagClientService(httpClient, _permissionService);
    }

    [Fact]
    public async Task IsFeatureEnabledAsync_WhenFlagActiveAndRing2_ShouldReturnTrue()
    {
        // Arrange
        var flags = new List<FeatureFlagClientDto>
        {
            new(
                Id: Guid.NewGuid(),
                Key: "backoffice.feature.plan_publishing",
                Name: "Publicação de Planos",
                Description: "Desc",
                Category: "CommercialAndPlans",
                IsEnabled: true,
                RolloutPercentage: 100,
                MaxAllowedRing: "Ring2_GeneralAvailability",
                WhitelistedEmails: new List<string>(),
                KillSwitchActive: false,
                KillSwitchReason: null,
                KillSwitchActivatedAtUtc: null,
                KillSwitchActivatedBy: null,
                CreatedAtUtc: DateTime.UtcNow,
                CreatedBy: "system",
                UpdatedAtUtc: null,
                UpdatedBy: null,
                LastToggledReason: null
            )
        };

        _permissionService.GetCurrentUserRoleAsync().Returns("SupportN1");
        var service = CreateService(flags);

        // Act
        var result = await service.IsFeatureEnabledAsync("backoffice.feature.plan_publishing");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsFeatureEnabledAsync_WhenKillSwitchActive_ShouldAlwaysReturnFalse()
    {
        // Arrange
        var flags = new List<FeatureFlagClientDto>
        {
            new(
                Id: Guid.NewGuid(),
                Key: "backoffice.feature.impersonation",
                Name: "Impersonação",
                Description: "Desc",
                Category: "SupportTools",
                IsEnabled: true,
                RolloutPercentage: 100,
                MaxAllowedRing: "Ring2_GeneralAvailability",
                WhitelistedEmails: new List<string>(),
                KillSwitchActive: true,
                KillSwitchReason: "Corte de emergência",
                KillSwitchActivatedAtUtc: DateTime.UtcNow,
                KillSwitchActivatedBy: "secops",
                CreatedAtUtc: DateTime.UtcNow,
                CreatedBy: "system",
                UpdatedAtUtc: null,
                UpdatedBy: null,
                LastToggledReason: null
            )
        };

        _permissionService.GetCurrentUserRoleAsync().Returns("PlatformOwner");
        var service = CreateService(flags);

        // Act
        var result = await service.IsFeatureEnabledAsync("backoffice.feature.impersonation");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsFeatureEnabledAsync_WhenRing0AndUserIsSupportN1_ShouldReturnFalse()
    {
        // Arrange
        var flags = new List<FeatureFlagClientDto>
        {
            new(
                Id: Guid.NewGuid(),
                Key: "backoffice.feature.compliance_unmasking",
                Name: "Unmasking LGPD",
                Description: "Desc",
                Category: "ComplianceAndPrivacy",
                IsEnabled: true,
                RolloutPercentage: 100,
                MaxAllowedRing: "Ring0_Canary",
                WhitelistedEmails: new List<string>(),
                KillSwitchActive: false,
                KillSwitchReason: null,
                KillSwitchActivatedAtUtc: null,
                KillSwitchActivatedBy: null,
                CreatedAtUtc: DateTime.UtcNow,
                CreatedBy: "system",
                UpdatedAtUtc: null,
                UpdatedBy: null,
                LastToggledReason: null
            )
        };

        _permissionService.GetCurrentUserRoleAsync().Returns("SupportN1");
        var service = CreateService(flags);

        // Act
        var result = await service.IsFeatureEnabledAsync("backoffice.feature.compliance_unmasking");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsFeatureEnabledAsync_WhenRing0AndUserIsPlatformOwner_ShouldReturnTrue()
    {
        // Arrange
        var flags = new List<FeatureFlagClientDto>
        {
            new(
                Id: Guid.NewGuid(),
                Key: "backoffice.feature.compliance_unmasking",
                Name: "Unmasking LGPD",
                Description: "Desc",
                Category: "ComplianceAndPrivacy",
                IsEnabled: true,
                RolloutPercentage: 100,
                MaxAllowedRing: "Ring0_Canary",
                WhitelistedEmails: new List<string>(),
                KillSwitchActive: false,
                KillSwitchReason: null,
                KillSwitchActivatedAtUtc: null,
                KillSwitchActivatedBy: null,
                CreatedAtUtc: DateTime.UtcNow,
                CreatedBy: "system",
                UpdatedAtUtc: null,
                UpdatedBy: null,
                LastToggledReason: null
            )
        };

        _permissionService.GetCurrentUserRoleAsync().Returns("PlatformOwner");
        var service = CreateService(flags);

        // Act
        var result = await service.IsFeatureEnabledAsync("backoffice.feature.compliance_unmasking");

        // Assert
        result.Should().BeTrue();
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly List<FeatureFlagClientDto> _flags;

        public MockHttpMessageHandler(List<FeatureFlagClientDto> flags)
        {
            _flags = flags;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(_flags)
            };
            return Task.FromResult(response);
        }
    }
}

using System.Net;
using CriaCerto.Modules.Backoffice.Application.Security;
using CriaCerto.Shared.UI.Abstractions;
using CriaCerto.Shared.UI.Models;
using CriaCerto.Shared.UI.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CriaCerto.Web.Client.UnitTests.Services;

public class ApiClientAuthorizationHeaderTests
{
    private readonly ITokenStorageService _tokenStorage;

    public ApiClientAuthorizationHeaderTests()
    {
        _tokenStorage = Substitute.For<ITokenStorageService>();
    }

    private class CaptureAuthorizationHandler : HttpMessageHandler
    {
        public HttpRequestMessage? CapturedRequest { get; private set; }
        private readonly string _responseJson;
        private readonly HttpStatusCode _statusCode;

        public CaptureAuthorizationHandler(string responseJson = "{}", HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _responseJson = responseJson;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedRequest = request;
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseJson, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    [Fact]
    public async Task PlantelApiClient_WhenTokenExists_ShouldAttachBearerToken()
    {
        // Arrange
        const string fakeToken = "jwt-plantel-token-123";
        _tokenStorage.GetTokenAsync().Returns(fakeToken);

        var handler = new CaptureAuthorizationHandler("{}");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new PlantelApiClient(httpClient, _tokenStorage);

        // Act
        await client.ListCowsAsync();

        // Assert
        handler.CapturedRequest.Should().NotBeNull();
        handler.CapturedRequest!.Headers.Authorization.Should().NotBeNull();
        handler.CapturedRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.CapturedRequest.Headers.Authorization.Parameter.Should().Be(fakeToken);
    }

    [Fact]
    public async Task PlantelApiClient_WhenTokenNull_ShouldNotAttachBearerToken()
    {
        // Arrange
        _tokenStorage.GetTokenAsync().Returns((string?)null);

        var handler = new CaptureAuthorizationHandler("{}");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new PlantelApiClient(httpClient, _tokenStorage);

        // Act
        await client.ListCowsAsync();

        // Assert
        handler.CapturedRequest.Should().NotBeNull();
        handler.CapturedRequest!.Headers.Authorization.Should().BeNull();
    }

    [Fact]
    public async Task BreedingOpsApiClient_WhenTokenExists_ShouldAttachBearerToken()
    {
        // Arrange
        const string fakeToken = "jwt-breeding-token-456";
        _tokenStorage.GetTokenAsync().Returns(fakeToken);

        var syncMock = Substitute.For<IOfflineSyncService>();
        var handler = new CaptureAuthorizationHandler("[]");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new BreedingOpsApiClient(httpClient, _tokenStorage, syncMock);

        // Act
        await client.GetIatfProtocolsAsync(Guid.NewGuid());

        // Assert
        handler.CapturedRequest.Should().NotBeNull();
        handler.CapturedRequest!.Headers.Authorization.Should().NotBeNull();
        handler.CapturedRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.CapturedRequest.Headers.Authorization.Parameter.Should().Be(fakeToken);
    }

    [Fact]
    public async Task GrowthApiClient_WhenTokenExists_ShouldAttachBearerToken()
    {
        // Arrange
        const string fakeToken = "jwt-growth-token-789";
        _tokenStorage.GetTokenAsync().Returns(fakeToken);

        var handler = new CaptureAuthorizationHandler("[]");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new GrowthApiClient(httpClient, _tokenStorage);

        // Act
        await client.GetPaddocksAsync(Guid.NewGuid());

        // Assert
        handler.CapturedRequest.Should().NotBeNull();
        handler.CapturedRequest!.Headers.Authorization.Should().NotBeNull();
        handler.CapturedRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.CapturedRequest.Headers.Authorization.Parameter.Should().Be(fakeToken);
    }

    [Fact]
    public async Task NutritionApiClient_WhenTokenExists_ShouldAttachBearerToken()
    {
        // Arrange
        const string fakeToken = "jwt-nutrition-token-101";
        _tokenStorage.GetTokenAsync().Returns(fakeToken);

        var handler = new CaptureAuthorizationHandler("[]");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new NutritionApiClient(httpClient, _tokenStorage);

        // Act
        await client.GetSilosAsync(Guid.NewGuid());

        // Assert
        handler.CapturedRequest.Should().NotBeNull();
        handler.CapturedRequest!.Headers.Authorization.Should().NotBeNull();
        handler.CapturedRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.CapturedRequest.Headers.Authorization.Parameter.Should().Be(fakeToken);
    }

    [Fact]
    public async Task CalvingApiClient_WhenTokenExists_ShouldAttachBearerToken()
    {
        // Arrange
        const string fakeToken = "jwt-calving-token-102";
        _tokenStorage.GetTokenAsync().Returns(fakeToken);

        var handler = new CaptureAuthorizationHandler("[]");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new CalvingApiClient(httpClient, _tokenStorage);

        // Act
        await client.GetCalvingRecordsAsync(Guid.NewGuid());

        // Assert
        handler.CapturedRequest.Should().NotBeNull();
        handler.CapturedRequest!.Headers.Authorization.Should().NotBeNull();
        handler.CapturedRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.CapturedRequest.Headers.Authorization.Parameter.Should().Be(fakeToken);
    }

    [Fact]
    public async Task SanitaryApiClient_WhenTokenExists_ShouldAttachBearerToken()
    {
        // Arrange
        const string fakeToken = "jwt-sanitary-token-103";
        _tokenStorage.GetTokenAsync().Returns(fakeToken);

        var handler = new CaptureAuthorizationHandler("[]");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new SanitaryApiClient(httpClient, _tokenStorage);

        // Act
        await client.GetActiveCampaignsAsync();

        // Assert
        handler.CapturedRequest.Should().NotBeNull();
        handler.CapturedRequest!.Headers.Authorization.Should().NotBeNull();
        handler.CapturedRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.CapturedRequest.Headers.Authorization.Parameter.Should().Be(fakeToken);
    }

    [Fact]
    public async Task AnalyticsApiClient_WhenTokenExists_ShouldAttachBearerToken()
    {
        // Arrange
        const string fakeToken = "jwt-analytics-token-104";
        _tokenStorage.GetTokenAsync().Returns(fakeToken);

        var handler = new CaptureAuthorizationHandler("{}");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new AnalyticsApiClient(httpClient, _tokenStorage);

        // Act
        await client.GetExecutiveDashboardAsync(Guid.NewGuid());

        // Assert
        handler.CapturedRequest.Should().NotBeNull();
        handler.CapturedRequest!.Headers.Authorization.Should().NotBeNull();
        handler.CapturedRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.CapturedRequest.Headers.Authorization.Parameter.Should().Be(fakeToken);
    }

    [Fact]
    public async Task TenancyApiClient_WhenTokenExists_ShouldAttachBearerToken()
    {
        // Arrange
        const string fakeToken = "jwt-tenancy-token-105";
        _tokenStorage.GetTokenAsync().Returns(fakeToken);

        var handler = new CaptureAuthorizationHandler("{}");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new TenancyApiClient(httpClient, _tokenStorage);

        // Act
        await client.GetTenantProfileAsync(Guid.NewGuid());

        // Assert
        handler.CapturedRequest.Should().NotBeNull();
        handler.CapturedRequest!.Headers.Authorization.Should().NotBeNull();
        handler.CapturedRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.CapturedRequest.Headers.Authorization.Parameter.Should().Be(fakeToken);
    }

    [Fact]
    public async Task BackofficeApiClient_WhenTokenExists_ShouldAttachBearerToken()
    {
        // Arrange
        const string fakeToken = "jwt-backoffice-token-106";
        _tokenStorage.GetTokenAsync().Returns(fakeToken);

        var handler = new CaptureAuthorizationHandler("{}");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new BackofficeApiClient(httpClient, _tokenStorage);

        // Act
        await client.GetDashboardKpisAsync();

        // Assert
        handler.CapturedRequest.Should().NotBeNull();
        handler.CapturedRequest!.Headers.Authorization.Should().NotBeNull();
        handler.CapturedRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.CapturedRequest.Headers.Authorization.Parameter.Should().Be(fakeToken);
    }

    [Fact]
    public async Task PartnerApiClient_WhenTokenExists_ShouldAttachBearerToken()
    {
        // Arrange
        const string fakeToken = "jwt-partner-token-107";
        _tokenStorage.GetTokenAsync().Returns(fakeToken);

        var handler = new CaptureAuthorizationHandler("{}");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new PartnerApiClient(httpClient, _tokenStorage);

        // Act
        await client.GetDashboardAsync();

        // Assert
        handler.CapturedRequest.Should().NotBeNull();
        handler.CapturedRequest!.Headers.Authorization.Should().NotBeNull();
        handler.CapturedRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.CapturedRequest.Headers.Authorization.Parameter.Should().Be(fakeToken);
    }

    [Fact]
    public async Task ReferralApiClient_WhenTokenExists_ShouldAttachBearerToken()
    {
        // Arrange
        const string fakeToken = "jwt-referral-token-108";
        _tokenStorage.GetTokenAsync().Returns(fakeToken);

        var handler = new CaptureAuthorizationHandler("{}");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new ReferralApiClient(httpClient, _tokenStorage);

        // Act
        await client.GetCustomerReferralDashboardAsync();

        // Assert
        handler.CapturedRequest.Should().NotBeNull();
        handler.CapturedRequest!.Headers.Authorization.Should().NotBeNull();
        handler.CapturedRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.CapturedRequest.Headers.Authorization.Parameter.Should().Be(fakeToken);
    }

    [Fact]
    public async Task FeatureFlagClientService_WhenTokenExists_ShouldAttachBearerToken()
    {
        // Arrange
        const string fakeToken = "jwt-ff-token-109";
        _tokenStorage.GetTokenAsync().Returns(fakeToken);

        var permissionService = Substitute.For<IBackofficePermissionService>();
        permissionService.GetCurrentUserRoleAsync().Returns("Admin");

        var handler = new CaptureAuthorizationHandler("[]");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var service = new FeatureFlagClientService(httpClient, permissionService, _tokenStorage);

        // Act
        await service.GetFlagsAsync(forceRefresh: true);

        // Assert
        handler.CapturedRequest.Should().NotBeNull();
        handler.CapturedRequest!.Headers.Authorization.Should().NotBeNull();
        handler.CapturedRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.CapturedRequest.Headers.Authorization.Parameter.Should().Be(fakeToken);
    }

    [Fact]
    public async Task OfflineSyncService_WhenTokenExists_ShouldAttachBearerToken()
    {
        // Arrange
        const string fakeToken = "jwt-sync-token-110";
        _tokenStorage.GetTokenAsync().Returns(fakeToken);

        var storage = Substitute.For<IOfflineSyncStorage>();
        var pendingOp = new SyncOperation
        {
            Id = Guid.NewGuid(),
            ModuleName = "Breeding",
            ActionType = "RegisterDiagnosis",
            PayloadJson = "{\"cowId\":\"" + Guid.NewGuid() + "\"}",
            CreatedAtUtc = DateTime.UtcNow,
            Status = SyncOperationStatus.Pending
        };

        storage.GetPendingOperationsAsync().Returns(new List<SyncOperation> { pendingOp });

        var handler = new CaptureAuthorizationHandler("{}");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var connectivity = Substitute.For<IConnectivityService>();
        connectivity.IsOnline.Returns(true);

        var syncService = new OfflineSyncService(httpClient, connectivity, storage, _tokenStorage);
        await syncService.InitializeAsync();

        // Act
        await syncService.ForceSyncAsync();

        // Assert
        handler.CapturedRequest.Should().NotBeNull();
        handler.CapturedRequest!.Headers.Authorization.Should().NotBeNull();
        handler.CapturedRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.CapturedRequest.Headers.Authorization.Parameter.Should().Be(fakeToken);
    }
}

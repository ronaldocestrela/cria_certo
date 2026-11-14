using System.Net;
using System.Text.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Web.Client.Models;
using CriaCerto.Web.Client.Services;
using FluentAssertions;
using Microsoft.JSInterop;
using NSubstitute;
using Xunit;

namespace CriaCerto.Web.Client.UnitTests.Services;

public class OfflineSyncServiceTests
{
    private readonly IJSRuntime _jsRuntimeMock;
    private readonly HttpMessageHandlerMock _httpHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly OfflineSyncService _sut;

    public OfflineSyncServiceTests()
    {
        _jsRuntimeMock = Substitute.For<IJSRuntime>();
        _httpHandlerMock = new HttpMessageHandlerMock();
        _httpClient = new HttpClient(_httpHandlerMock)
        {
            BaseAddress = new Uri("https://localhost:7000/")
        };

        _sut = new OfflineSyncService(_jsRuntimeMock, _httpClient);
    }

    [Fact]
    public void SetNetworkStatus_ShouldUpdateIsOnline_AndRaiseOnStateChanged()
    {
        // Arrange
        bool eventRaised = false;
        _sut.OnStateChanged += () => eventRaised = true;

        // Act
        _sut.SetNetworkStatus(false);

        // Assert
        _sut.IsOnline.Should().BeFalse();
        eventRaised.Should().BeTrue();
    }

    [Fact]
    public async Task EnqueueOperationAsync_ShouldIncrementPendingCount()
    {
        // Arrange
        var payload = new { AnimalId = Guid.NewGuid(), WeightKg = 450.5 };

        // Act
        var result = await _sut.EnqueueOperationAsync("Growth", "RecordWeighing", payload);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _sut.PendingCount.Should().Be(1);
        _sut.PendingOperations[0].ModuleName.Should().Be("Growth");
        _sut.PendingOperations[0].ActionType.Should().Be("RecordWeighing");
    }

    [Fact]
    public async Task ForceSyncAsync_ShouldSendRawJsonPayload_AndRemoveOnSuccess()
    {
        // Arrange
        _httpHandlerMock.ResponseStatusCode = HttpStatusCode.OK;
        await _sut.EnqueueOperationAsync("Breeding", "RecordInsemination", new { CowTag = "BR100" });
        _sut.SetNetworkStatus(true);

        // Act
        var result = await _sut.ForceSyncAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        _sut.PendingCount.Should().Be(0);
        _httpHandlerMock.LastRequestUri.Should().NotBeNull();
        _httpHandlerMock.LastRequestUri!.PathAndQuery.Should().Be("/api/v1/sync/Breeding/RecordInsemination");
        _httpHandlerMock.LastRequestContent.Should().Be("{\"CowTag\":\"BR100\"}");
    }

    [Fact]
    public async Task ForceSyncAsync_ShouldDetectConflict_WhenServerReturnsConflictStatusCode()
    {
        // Arrange
        _httpHandlerMock.ResponseStatusCode = HttpStatusCode.Conflict;
        _httpHandlerMock.ResponseBody = JsonSerializer.Serialize(new { ServerCowStatus = "Gestation" });
        
        await _sut.EnqueueOperationAsync("Breeding", "RecordInsemination", new { CowTag = "BR100", LocalStatus = "Empty" });
        _sut.SetNetworkStatus(true);

        // Act
        var result = await _sut.ForceSyncAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Sync.ConflictDetected");
        _sut.ActiveConflicts.Should().HaveCount(1);
    }

    [Fact]
    public async Task ResolveConflictAsync_UseLocal_ShouldSendForceTrue_AndClearConflictOnSuccess()
    {
        // Arrange
        _httpHandlerMock.ResponseStatusCode = HttpStatusCode.Conflict;
        _httpHandlerMock.ResponseBody = JsonSerializer.Serialize(new { ServerCowStatus = "Gestation" });

        await _sut.EnqueueOperationAsync("Breeding", "RecordInsemination", new { CowTag = "BR100" });
        await _sut.ForceSyncAsync();

        var conflict = _sut.ActiveConflicts[0];
        _httpHandlerMock.ResponseStatusCode = HttpStatusCode.OK;

        // Act
        var result = await _sut.ResolveConflictAsync(conflict.OperationId, ConflictResolutionOption.UseLocal);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _httpHandlerMock.LastRequestUri.Should().NotBeNull();
        _httpHandlerMock.LastRequestUri!.PathAndQuery.Should().Be("/api/v1/sync/Breeding/RecordInsemination?force=true");
        _httpHandlerMock.LastRequestContent.Should().Be("{\"CowTag\":\"BR100\"}");
        _sut.ActiveConflicts.Should().BeEmpty();
        _sut.PendingCount.Should().Be(0);
    }

    [Fact]
    public async Task ResolveConflictAsync_UseServer_ShouldDiscardLocalOperation()
    {
        // Arrange
        _httpHandlerMock.ResponseStatusCode = HttpStatusCode.Conflict;
        _httpHandlerMock.ResponseBody = JsonSerializer.Serialize(new { ServerCowStatus = "Gestation" });
        
        await _sut.EnqueueOperationAsync("Breeding", "RecordInsemination", new { CowTag = "BR100" });
        await _sut.ForceSyncAsync();

        var conflict = _sut.ActiveConflicts[0];

        // Act
        var result = await _sut.ResolveConflictAsync(conflict.OperationId, ConflictResolutionOption.UseServer);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _sut.ActiveConflicts.Should().BeEmpty();
        _sut.PendingCount.Should().Be(0);
    }

    [Fact]
    public async Task ClearPendingOperationsAsync_ShouldClearAllOperationsAndConflicts()
    {
        // Arrange
        await _sut.EnqueueOperationAsync("Sanitary", "ApplyVaccine", new { Vaccine = "Febre Aftosa" });

        // Act
        var result = await _sut.ClearPendingOperationsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        _sut.PendingCount.Should().Be(0);
        _sut.ActiveConflicts.Should().BeEmpty();
    }

    private class HttpMessageHandlerMock : HttpMessageHandler
    {
        public HttpStatusCode ResponseStatusCode { get; set; } = HttpStatusCode.OK;
        public string ResponseBody { get; set; } = "{}";
        public Uri? LastRequestUri { get; private set; }
        public string? LastRequestContent { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastRequestContent = request.Content is not null
                ? await request.Content.ReadAsStringAsync(cancellationToken)
                : null;

            var response = new HttpResponseMessage(ResponseStatusCode)
            {
                Content = new StringContent(ResponseBody)
            };
            return response;
        }
    }
}

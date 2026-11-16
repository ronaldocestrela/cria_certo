using CriaCerto.Shared.UI.Models;
using CriaCerto.Web.Client.Services.Browser;
using FluentAssertions;
using Microsoft.JSInterop;
using NSubstitute;
using Xunit;

namespace CriaCerto.Web.Client.UnitTests.Services;

public class BrowserPlatformServicesTests
{
    private readonly IJSRuntime _jsRuntimeMock;

    public BrowserPlatformServicesTests()
    {
        _jsRuntimeMock = Substitute.For<IJSRuntime>();
    }

    [Fact]
    public async Task BrowserTokenStorageService_SetAndRemove_ShouldNotThrow()
    {
        // Arrange
        var service = new BrowserTokenStorageService(_jsRuntimeMock);

        // Act & Assert (não deve lançar exceção mesmo em ambiente unitário não-browser)
        await service.SetTokenAsync("sample.jwt.token");
        await service.RemoveTokenAsync();
        var token = await service.GetTokenAsync();

        // No ambiente unitário (Linux/dotnet test), OperatingSystem.IsBrowser() é false
        token.Should().BeNull();
    }

    [Fact]
    public async Task BrowserFileExportService_ExportFileAsync_ShouldNotThrow()
    {
        // Arrange
        var service = new BrowserFileExportService(_jsRuntimeMock);

        // Act & Assert
        var act = () => service.ExportFileAsync("relatorio.csv", "text/csv", [1, 2, 3]);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task BrowserConnectivityService_WhenNotBrowser_ShouldDefaultToOnline()
    {
        // Arrange
        using var service = new BrowserConnectivityService(_jsRuntimeMock);

        // Act
        await service.InitializeAsync();

        // Assert
        service.IsOnline.Should().BeTrue();
    }

    [Fact]
    public void BrowserConnectivityService_OnNetworkStatusChanged_ShouldUpdateAndRaiseEvent()
    {
        // Arrange
        using var service = new BrowserConnectivityService(_jsRuntimeMock);
        bool? receivedStatus = null;
        service.ConnectivityChanged += status => receivedStatus = status;

        // Act
        service.OnNetworkStatusChanged(false);

        // Assert
        service.IsOnline.Should().BeFalse();
        receivedStatus.Should().BeFalse();

        service.OnNetworkStatusChanged(true);
        service.IsOnline.Should().BeTrue();
        receivedStatus.Should().BeTrue();
    }

    [Fact]
    public async Task BrowserOfflineSyncStorage_WhenNotBrowser_ShouldReturnEmptyAndNotThrow()
    {
        // Arrange
        var storage = new BrowserOfflineSyncStorage(_jsRuntimeMock);

        // Act
        await storage.InitializeAsync();
        var items = await storage.GetPendingOperationsAsync();
        await storage.SavePendingOperationAsync(new SyncOperation { Id = Guid.NewGuid() });
        await storage.RemovePendingOperationAsync(Guid.NewGuid());
        await storage.ClearPendingOperationsAsync();

        // Assert
        items.Should().BeEmpty();
    }
}


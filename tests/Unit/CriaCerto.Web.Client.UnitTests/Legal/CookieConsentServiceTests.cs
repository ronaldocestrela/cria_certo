using System.Text.Json;
using CriaCerto.Web.Client.Models;
using CriaCerto.Web.Client.Services;
using FluentAssertions;
using Microsoft.JSInterop;
using NSubstitute;
using Xunit;

namespace CriaCerto.Web.Client.UnitTests.Legal;

public class CookieConsentServiceTests
{
    private readonly IJSRuntime _jsRuntime;
    private readonly CookieConsentService _sut;

    public CookieConsentServiceTests()
    {
        _jsRuntime = Substitute.For<IJSRuntime>();
        _sut = new CookieConsentService(_jsRuntime);
    }

    [Fact]
    public void CookieConsentState_CreateDefaultAccepted_ShouldHave180DaysTtlAndAllCategories()
    {
        // Arrange
        var now = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var state = CookieConsentState.CreateDefaultAccepted(now);

        // Assert
        state.Should().NotBeNull();
        state.IsAccepted.Should().BeTrue();
        state.AcceptedAtUtc.Should().Be(now);
        state.ExpiresAtUtc.Should().Be(now.AddDays(180));
        state.Version.Should().Be("1.0");
        state.Essential.Should().BeTrue();
        state.OfflineStorage.Should().BeTrue();
        state.ReferralTracking.Should().BeTrue();
    }

    [Fact]
    public void CookieConsentState_IsValid_WhenNotAccepted_ShouldReturnFalse()
    {
        // Arrange
        var state = new CookieConsentState
        {
            IsAccepted = false,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(10),
            Version = "1.0"
        };

        // Act & Assert
        state.IsValid().Should().BeFalse();
    }

    [Fact]
    public void CookieConsentState_IsValid_WhenExpired_ShouldReturnFalse()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var state = new CookieConsentState
        {
            IsAccepted = true,
            AcceptedAtUtc = now.AddDays(-200),
            ExpiresAtUtc = now.AddDays(-20),
            Version = "1.0"
        };

        // Act & Assert
        state.IsValid(now).Should().BeFalse();
    }

    [Fact]
    public void CookieConsentState_IsValid_WhenVersionMismatch_ShouldReturnFalse()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var state = new CookieConsentState
        {
            IsAccepted = true,
            AcceptedAtUtc = now,
            ExpiresAtUtc = now.AddDays(180),
            Version = "0.9"
        };

        // Act & Assert
        state.IsValid(now, expectedVersion: "1.0").Should().BeFalse();
    }

    [Fact]
    public void CookieConsentState_IsValid_WhenValid_ShouldReturnTrue()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var state = CookieConsentState.CreateDefaultAccepted(now);

        // Act & Assert
        state.IsValid(now, "1.0").Should().BeTrue();
    }

    [Fact]
    public void CookieConsentCatalog_ShouldDefineMandatoryCategories()
    {
        // Act
        var categories = CookieConsentCatalog.Categories;

        // Assert
        categories.Should().NotBeEmpty();
        categories.Should().Contain(c => c.Id == "essential" && c.IsRequired);
        categories.Should().Contain(c => c.Id == "pwa_offline" && c.IsRequired);
        categories.Should().Contain(c => c.Id == "referral" && !c.IsRequired);
        CookieConsentCatalog.StorageKey.Should().Be("cria_certo_cookie_consent_v1");
    }

    [Fact]
    public async Task HasValidConsentAsync_WhenStorageIsEmpty_ShouldReturnFalse()
    {
        // Arrange
        _jsRuntime.InvokeAsync<string?>(
            "localStorage.getItem",
            Arg.Is<object?[]>(args => args != null && (string)args[0]! == CookieConsentCatalog.StorageKey)
        ).Returns(new ValueTask<string?>((string?)null));

        // Act
        var result = await _sut.HasValidConsentAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasValidConsentAsync_WhenStorageHasExpiredConsent_ShouldReturnFalse()
    {
        // Arrange
        var expiredState = new CookieConsentState
        {
            IsAccepted = true,
            AcceptedAtUtc = DateTime.UtcNow.AddDays(-190),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(-10),
            Version = "1.0"
        };
        var json = JsonSerializer.Serialize(expiredState);

        _jsRuntime.InvokeAsync<string?>(
            "localStorage.getItem",
            Arg.Is<object?[]>(args => args != null && (string)args[0]! == CookieConsentCatalog.StorageKey)
        ).Returns(new ValueTask<string?>(json));

        // Act
        var result = await _sut.HasValidConsentAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasValidConsentAsync_WhenStorageHasValidConsent_ShouldReturnTrue()
    {
        // Arrange
        var validState = CookieConsentState.CreateDefaultAccepted();
        var json = JsonSerializer.Serialize(validState);

        _jsRuntime.InvokeAsync<string?>(
            "localStorage.getItem",
            Arg.Is<object?[]>(args => args != null && (string)args[0]! == CookieConsentCatalog.StorageKey)
        ).Returns(new ValueTask<string?>(json));

        // Act
        var result = await _sut.HasValidConsentAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasValidConsentAsync_WhenCorruptedJsonInStorage_ShouldHandleGracefullyAndReturnFalse()
    {
        // Arrange
        _jsRuntime.InvokeAsync<string?>(
            "localStorage.getItem",
            Arg.Is<object?[]>(args => args != null && (string)args[0]! == CookieConsentCatalog.StorageKey)
        ).Returns(new ValueTask<string?>("{ corrupted invalid json ..."));

        // Act
        var result = await _sut.HasValidConsentAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SaveConsentAsync_ShouldPersistStateAndNotifySubscribers()
    {
        // Arrange
        bool eventFired = false;
        _sut.OnConsentChanged += () => eventFired = true;

        // Act
        await _sut.SaveConsentAsync();

        // Assert
        eventFired.Should().BeTrue();
        var hasConsent = await _sut.HasValidConsentAsync();
        hasConsent.Should().BeTrue();

        var state = await _sut.GetConsentAsync();
        state.Should().NotBeNull();
        state!.IsAccepted.Should().BeTrue();
        state.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow.AddDays(170));
    }

    [Fact]
    public async Task ResetConsentAsync_ShouldClearStateAndNotifySubscribers()
    {
        // Arrange
        await _sut.SaveConsentAsync();
        bool eventFired = false;
        _sut.OnConsentChanged += () => eventFired = true;

        // Act
        await _sut.ResetConsentAsync();

        // Assert
        eventFired.Should().BeTrue();
        var hasConsent = await _sut.HasValidConsentAsync();
        hasConsent.Should().BeFalse();

        var state = await _sut.GetConsentAsync();
        state.Should().BeNull();
    }
}

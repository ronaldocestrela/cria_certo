using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CriaCerto.Shared.UI.Abstractions;
using CriaCerto.Shared.UI.Auth;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CriaCerto.Web.Client.UnitTests.Auth;

public class CustomAuthStateProviderWithTokenStorageTests
{
    private readonly ITokenStorageService _tokenStorageMock;
    private readonly CustomAuthStateProvider _sut;

    public CustomAuthStateProviderWithTokenStorageTests()
    {
        _tokenStorageMock = Substitute.For<ITokenStorageService>();
        _sut = new CustomAuthStateProvider(_tokenStorageMock);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_WhenTokenIsNull_ShouldReturnAnonymous()
    {
        // Arrange
        _tokenStorageMock.GetTokenAsync().Returns((string?)null);

        // Act
        var state = await _sut.GetAuthenticationStateAsync();

        // Assert
        state.User.Identity?.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_WhenTokenIsValid_ShouldReturnAuthenticatedUser()
    {
        // Arrange
        var token = CreateJwt(new Dictionary<string, object>
        {
            ["sub"] = "user-abc",
            ["email"] = "veterinario@criacerto.com.br",
            ["exp"] = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds()
        });
        _tokenStorageMock.GetTokenAsync().Returns(token);

        // Act
        var state = await _sut.GetAuthenticationStateAsync();

        // Assert
        state.User.Identity?.IsAuthenticated.Should().BeTrue();
        state.User.FindFirst(ClaimTypes.NameIdentifier)?.Value.Should().Be("user-abc");
        state.User.FindFirst(ClaimTypes.Email)?.Value.Should().Be("veterinario@criacerto.com.br");
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_WhenTokenIsExpired_ShouldRemoveTokenAndReturnAnonymous()
    {
        // Arrange
        var expiredToken = CreateJwt(new Dictionary<string, object>
        {
            ["sub"] = "user-expired",
            ["exp"] = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds()
        });
        _tokenStorageMock.GetTokenAsync().Returns(expiredToken);

        // Act
        var state = await _sut.GetAuthenticationStateAsync();

        // Assert
        state.User.Identity?.IsAuthenticated.Should().BeFalse();
        await _tokenStorageMock.Received(1).RemoveTokenAsync();
    }

    [Fact]
    public async Task MarkUserAsAuthenticated_ShouldSaveTokenAndNotifyState()
    {
        // Arrange
        var token = CreateJwt(new Dictionary<string, object>
        {
            ["sub"] = "user-login",
            ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()
        });

        _tokenStorageMock.When(x => x.SetTokenAsync(Arg.Any<string>()))
            .Do(x => _tokenStorageMock.GetTokenAsync().Returns(x.Arg<string>()));

        bool notificationFired = false;
        _sut.AuthenticationStateChanged += task =>
        {
            notificationFired = true;
        };

        // Act
        await _sut.MarkUserAsAuthenticated(token);

        // Assert
        await _tokenStorageMock.Received(1).SetTokenAsync(token);
        notificationFired.Should().BeTrue();
        var state = await _sut.GetAuthenticationStateAsync();
        state.User.Identity?.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public async Task MarkUserAsLoggedOut_ShouldRemoveTokenAndNotifyAnonymous()
    {
        // Arrange
        _tokenStorageMock.When(x => x.RemoveTokenAsync())
            .Do(_ => _tokenStorageMock.GetTokenAsync().Returns((string?)null));

        bool notificationFired = false;
        _sut.AuthenticationStateChanged += task =>
        {
            notificationFired = true;
        };

        // Act
        await _sut.MarkUserAsLoggedOut();

        // Assert
        await _tokenStorageMock.Received(1).RemoveTokenAsync();
        notificationFired.Should().BeTrue();
        var state = await _sut.GetAuthenticationStateAsync();
        state.User.Identity?.IsAuthenticated.Should().BeFalse();
    }

    private static string CreateJwt(Dictionary<string, object> payload)
    {
        var header = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"alg\":\"none\",\"typ\":\"JWT\"}"));
        var payloadJson = JsonSerializer.Serialize(payload);
        var payloadSegment = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson));
        return $"{header}.{payloadSegment}.signature";
    }
}

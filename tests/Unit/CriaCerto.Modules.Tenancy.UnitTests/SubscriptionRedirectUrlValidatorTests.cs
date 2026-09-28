using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Services;
using FluentAssertions;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class SubscriptionRedirectUrlValidatorTests
{
    private readonly SubscriptionUrlValidator _validator;

    public SubscriptionRedirectUrlValidatorTests()
    {
        var allowedOrigins = new[]
        {
            "http://localhost:8081",
            "https://app.criacerto.com.br",
            "https://criacerto.com.br"
        };
        _validator = new SubscriptionUrlValidator(allowedOrigins, "http://localhost:8081");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsAllowedUrl_Should_Return_True_For_Null_Or_Whitespace(string? url)
    {
        var result = _validator.IsAllowedUrl(url);
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("http://localhost:8081")]
    [InlineData("http://localhost:8081/")]
    [InlineData("http://localhost:8081/settings/subscription?success=true")]
    [InlineData("https://app.criacerto.com.br/billing/success")]
    [InlineData("https://criacerto.com.br/settings/subscription?canceled=true")]
    public void IsAllowedUrl_Should_Return_True_For_Allowed_Origins(string url)
    {
        var result = _validator.IsAllowedUrl(url);
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("/settings/subscription")]
    [InlineData("/settings/subscription?success=true")]
    [InlineData("/portal/return")]
    public void IsAllowedUrl_Should_Return_True_For_Relative_Paths(string url)
    {
        var result = _validator.IsAllowedUrl(url);
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("https://evil.com")]
    [InlineData("https://evil.com/phishing")]
    [InlineData("http://localhost:9999/hack")]
    [InlineData("https://phishing-criacerto.com")]
    public void IsAllowedUrl_Should_Return_False_For_Unauthorized_Domains(string url)
    {
        var result = _validator.IsAllowedUrl(url);
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("https://app.criacerto.com.br.evil.com")]
    [InlineData("http://localhost:8081.evil.com")]
    [InlineData("https://evil.com?app.criacerto.com.br")]
    public void IsAllowedUrl_Should_Return_False_For_Subdomain_Bypass_Attempts(string url)
    {
        var result = _validator.IsAllowedUrl(url);
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("https://app.criacerto.com.br@evil.com/path")]
    [InlineData("http://user:pass@localhost:8081/path")]
    public void IsAllowedUrl_Should_Return_False_For_UserInfo_Bypass_Attempts(string url)
    {
        var result = _validator.IsAllowedUrl(url);
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("//evil.com/phishing")]
    [InlineData(@"\\evil.com\phishing")]
    [InlineData("javascript:alert(document.cookie)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("ftp://criacerto.com.br/file")]
    public void IsAllowedUrl_Should_Return_False_For_Dangerous_Schemes_And_Protocol_Relative(string url)
    {
        var result = _validator.IsAllowedUrl(url);
        result.Should().BeFalse();
    }

    [Fact]
    public void ResolveSafeUrl_Should_Return_Fallback_When_RequestedUrl_Is_Null_Or_Whitespace()
    {
        const string fallback = "http://localhost:8081/settings/subscription";
        var result = _validator.ResolveSafeUrl(null, fallback);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(fallback);
    }

    [Fact]
    public void ResolveSafeUrl_Should_Return_Requested_Url_When_Whitelisted_Absolute_Url()
    {
        const string requested = "https://app.criacerto.com.br/settings/subscription?success=true";
        const string fallback = "http://localhost:8081/settings/subscription";

        var result = _validator.ResolveSafeUrl(requested, fallback);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(requested);
    }

    [Fact]
    public void ResolveSafeUrl_Should_Prefix_Relative_Path_With_Authority()
    {
        const string relative = "/settings/subscription?success=true";
        const string fallback = "http://localhost:8081/settings/subscription";

        var result = _validator.ResolveSafeUrl(relative, fallback);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("http://localhost:8081/settings/subscription?success=true");
    }

    [Fact]
    public void ResolveSafeUrl_Should_Fail_With_Validation_Error_When_Domain_Is_Unauthorized()
    {
        const string evilUrl = "https://evil.attacker.com/steal-token";
        const string fallback = "http://localhost:8081/settings/subscription";

        var result = _validator.ResolveSafeUrl(evilUrl, fallback);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Subscription.InvalidRedirectUrl");
    }
}

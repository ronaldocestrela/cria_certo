using CriaCerto.Modules.Tenancy.Application.Features.SubscriptionCheckout;
using CriaCerto.Modules.Tenancy.Application.Features.SubscriptionPortal;
using CriaCerto.Modules.Tenancy.Application.Features.UpdateModularSubscription;
using CriaCerto.Modules.Tenancy.Application.Services;
using FluentAssertions;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class SubscriptionCommandValidatorsTests
{
    private readonly SubscriptionUrlValidator _urlValidator;
    private readonly CreateCheckoutSessionCommandValidator _checkoutValidator;
    private readonly CreatePortalSessionCommandValidator _portalValidator;
    private readonly UpdateModularSubscriptionCommandValidator _modularUpdateValidator;

    public SubscriptionCommandValidatorsTests()
    {
        var allowedOrigins = new[] { "http://localhost:8081", "https://criacerto.com.br" };
        _urlValidator = new SubscriptionUrlValidator(allowedOrigins, "http://localhost:8081");
        _checkoutValidator = new CreateCheckoutSessionCommandValidator(_urlValidator);
        _portalValidator = new CreatePortalSessionCommandValidator(_urlValidator);
        _modularUpdateValidator = new UpdateModularSubscriptionCommandValidator();
    }

    [Fact]
    public void CheckoutValidator_Should_Pass_For_Valid_Command_With_Allowed_Urls()
    {
        var command = new CreateCheckoutSessionCommand(
            TenantId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            PlanId: "Starter",
            BillingCycle: "monthly",
            SuccessUrl: "http://localhost:8081/settings/subscription?success=true",
            CancelUrl: "http://localhost:8081/settings/subscription?canceled=true"
        );

        var result = _checkoutValidator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CheckoutValidator_Should_Pass_For_Valid_Modular_Command()
    {
        var command = new CreateCheckoutSessionCommand(
            TenantId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            HeadTierCode: "TIER_1500",
            SelectedModules: ["Cows", "Breeding", "Sanitary"],
            BillingCycle: "monthly"
        );

        var result = _checkoutValidator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CheckoutValidator_Should_Fail_For_Invalid_Selected_Module()
    {
        var command = new CreateCheckoutSessionCommand(
            TenantId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            HeadTierCode: "TIER_1500",
            SelectedModules: ["Cows", "ModuloInvalido"],
            BillingCycle: "monthly"
        );

        var result = _checkoutValidator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateCheckoutSessionCommand.SelectedModules));
    }

    [Fact]
    public void CheckoutValidator_Should_Fail_When_Both_HeadTierCode_And_PlanId_Are_Empty()
    {
        var command = new CreateCheckoutSessionCommand(
            TenantId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            HeadTierCode: "",
            SelectedModules: ["Cows"],
            BillingCycle: "monthly",
            PlanId: null
        );

        var result = _checkoutValidator.Validate(command);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void CheckoutValidator_Should_Pass_When_Urls_Are_Null_Or_Relative()
    {
        var command = new CreateCheckoutSessionCommand(
            TenantId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            PlanId: "Pro",
            BillingCycle: "annual",
            SuccessUrl: "/settings/subscription?success=true",
            CancelUrl: null
        );

        var result = _checkoutValidator.Validate(command);
        result.IsValid.Should().BeTrue();
    }


    [Theory]
    [InlineData("https://phishing.com/login", null)]
    [InlineData(null, "https://evil.org/logout")]
    [InlineData("//malicious.com", "http://localhost:8081")]
    [InlineData("javascript:stealCookie()", null)]
    public void CheckoutValidator_Should_Fail_When_Urls_Are_Not_Whitelisted(string? successUrl, string? cancelUrl)
    {
        var command = new CreateCheckoutSessionCommand(
            TenantId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            PlanId: "Starter",
            BillingCycle: "monthly",
            SuccessUrl: successUrl,
            CancelUrl: cancelUrl
        );

        var result = _checkoutValidator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateCheckoutSessionCommand.SuccessUrl) ||
                                            e.PropertyName == nameof(CreateCheckoutSessionCommand.CancelUrl));
    }

    [Fact]
    public void PortalValidator_Should_Pass_For_Valid_Command_And_Allowed_ReturnUrl()
    {
        var command = new CreatePortalSessionCommand(
            TenantId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            ReturnUrl: "https://criacerto.com.br/settings/subscription"
        );

        var result = _portalValidator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void PortalValidator_Should_Pass_For_Relative_Or_Null_ReturnUrl()
    {
        var command = new CreatePortalSessionCommand(
            TenantId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            ReturnUrl: "/settings/subscription"
        );

        var result = _portalValidator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("https://evil.com/fake-portal")]
    [InlineData("//attacker.com")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    public void PortalValidator_Should_Fail_When_ReturnUrl_Is_Unauthorized(string returnUrl)
    {
        var command = new CreatePortalSessionCommand(
            TenantId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            ReturnUrl: returnUrl
        );

        var result = _portalValidator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreatePortalSessionCommand.ReturnUrl));
    }

    [Fact]
    public void PortalValidator_Should_Pass_For_Production_ReturnUrl_Even_With_Local_Only_Origins_Configured()
    {
        // Simula ambiente onde a configuração forneceu apenas localhost
        var localOnlyValidator = new SubscriptionUrlValidator(new[] { "http://localhost:5205" });
        var validator = new CreatePortalSessionCommandValidator(localOnlyValidator);

        var command = new CreatePortalSessionCommand(
            TenantId: Guid.Parse("6ec72352-5404-4397-97a3-b18816b76560"),
            UserId: Guid.NewGuid(),
            ReturnUrl: "https://criacerto.com.br/settings/subscription?success=true"
        );

        var result = validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ModularUpdateValidator_Should_Pass_For_Valid_Command()
    {
        var command = new UpdateModularSubscriptionCommand(
            TenantId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            HeadTierCode: "TIER_1500",
            SelectedModules: ["Cows", "Breeding", "Sanitary"],
            BillingCycle: "annual",
            AllowGracePeriod: true
        );

        var result = _modularUpdateValidator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ModularUpdateValidator_Should_Pass_When_HeadTierCode_Is_Null_Or_Empty()
    {
        var command = new UpdateModularSubscriptionCommand(
            TenantId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            HeadTierCode: null,
            SelectedModules: ["Cows", "Breeding"]
        );

        var result = _modularUpdateValidator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ModularUpdateValidator_Should_Fail_When_TenantId_Is_Empty()
    {
        var command = new UpdateModularSubscriptionCommand(
            TenantId: Guid.Empty,
            UserId: Guid.NewGuid(),
            HeadTierCode: "TIER_500",
            SelectedModules: ["Cows"]
        );

        var result = _modularUpdateValidator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateModularSubscriptionCommand.TenantId));
    }

    [Fact]
    public void ModularUpdateValidator_Should_Fail_When_UserId_Is_Empty()
    {
        var command = new UpdateModularSubscriptionCommand(
            TenantId: Guid.NewGuid(),
            UserId: Guid.Empty,
            HeadTierCode: "TIER_500",
            SelectedModules: ["Cows"]
        );

        var result = _modularUpdateValidator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateModularSubscriptionCommand.UserId));
    }

    [Fact]
    public void ModularUpdateValidator_Should_Fail_When_HeadTierCode_Is_Invalid()
    {
        var command = new UpdateModularSubscriptionCommand(
            TenantId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            HeadTierCode: "TIER_INEXISTENTE",
            SelectedModules: ["Cows"]
        );

        var result = _modularUpdateValidator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateModularSubscriptionCommand.HeadTierCode));
    }

    [Fact]
    public void ModularUpdateValidator_Should_Fail_When_SelectedModule_Is_Invalid()
    {
        var command = new UpdateModularSubscriptionCommand(
            TenantId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            HeadTierCode: "TIER_500",
            SelectedModules: ["Cows", "ModuloInvalido"]
        );

        var result = _modularUpdateValidator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateModularSubscriptionCommand.SelectedModules));
    }

    [Fact]
    public void ModularUpdateValidator_Should_Fail_When_BillingCycle_Is_Invalid()
    {
        var command = new UpdateModularSubscriptionCommand(
            TenantId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            HeadTierCode: "TIER_500",
            SelectedModules: ["Cows"],
            BillingCycle: "quinzenal"
        );

        var result = _modularUpdateValidator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateModularSubscriptionCommand.BillingCycle));
    }
}

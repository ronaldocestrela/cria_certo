using CriaCerto.BuildingBlocks.Abstractions.Licensing;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using FluentValidation;

namespace CriaCerto.Modules.Tenancy.Application.Features.SubscriptionCheckout;

public sealed class CreateCheckoutSessionCommandValidator : AbstractValidator<CreateCheckoutSessionCommand>
{
    public CreateCheckoutSessionCommandValidator(ISubscriptionUrlValidator urlValidator)
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId é obrigatório.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId é obrigatório.");

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.HeadTierCode) || !string.IsNullOrWhiteSpace(x.PlanId))
            .WithMessage("HeadTierCode ou PlanId é obrigatório.");

        RuleFor(x => x.HeadTierCode)
            .Must(code => string.IsNullOrWhiteSpace(code) ||
                          HeadTierCapacityLimits.IsValidHeadTierCode(code) ||
                          !string.IsNullOrWhiteSpace(ModuleLicenseChecker.NormalizePlan(code)))
            .WithMessage("Faixa de cabeças (HeadTierCode) inválida.");

        RuleFor(x => x.SelectedModules)
            .Must(modules => modules == null || modules.All(HeadTierCapacityLimits.IsValidCommercialModuleKey))
            .WithMessage("Um ou mais módulos selecionados são inválidos.");

        RuleFor(x => x.BillingCycle)
            .Must(cycle => string.Equals(cycle, "monthly", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(cycle, "mensal", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(cycle, "annual", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(cycle, "anual", StringComparison.OrdinalIgnoreCase))
            .WithMessage("BillingCycle deve ser 'monthly' ou 'annual'.");

        RuleFor(x => x.SuccessUrl)
            .Must(url => string.IsNullOrWhiteSpace(url) || urlValidator.IsAllowedUrl(url))
            .WithMessage("SuccessUrl contém um domínio não autorizado ou formato inválido (Open Redirect prevenido).");

        RuleFor(x => x.CancelUrl)
            .Must(url => string.IsNullOrWhiteSpace(url) || urlValidator.IsAllowedUrl(url))
            .WithMessage("CancelUrl contém um domínio não autorizado ou formato inválido (Open Redirect prevenido).");
    }
}

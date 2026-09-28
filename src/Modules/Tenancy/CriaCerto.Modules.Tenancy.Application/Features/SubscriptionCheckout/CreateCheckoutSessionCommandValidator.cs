using CriaCerto.Modules.Tenancy.Application.Abstractions;
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

        RuleFor(x => x.PlanId)
            .NotEmpty().WithMessage("PlanId é obrigatório.");

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

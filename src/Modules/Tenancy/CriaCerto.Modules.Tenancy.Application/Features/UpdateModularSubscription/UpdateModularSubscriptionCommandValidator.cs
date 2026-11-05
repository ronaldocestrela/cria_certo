using CriaCerto.Modules.Tenancy.Application.Domain;
using FluentValidation;

namespace CriaCerto.Modules.Tenancy.Application.Features.UpdateModularSubscription;

public sealed class UpdateModularSubscriptionCommandValidator : AbstractValidator<UpdateModularSubscriptionCommand>
{
    private static readonly string[] AllowedBillingCycles = ["monthly", "annual", "mensal", "anual"];

    public UpdateModularSubscriptionCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("O identificador da fazenda/organização (TenantId) é obrigatório.");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("O identificador do usuário solicitante (UserId) é obrigatório.");

        When(x => !string.IsNullOrWhiteSpace(x.HeadTierCode), () =>
        {
            RuleFor(x => x.HeadTierCode!)
                .Must(PlanCapacityLimits.IsValidHeadTierCode)
                .WithMessage("A faixa de capacidade de cabeças (HeadTierCode) informada é inválida.");
        });

        When(x => x.SelectedModules != null, () =>
        {
            RuleFor(x => x.SelectedModules!)
                .Must(modules => modules.All(m => !string.IsNullOrWhiteSpace(m) && PlanCapacityLimits.NormalizeCommercialModuleKey(m) != null))
                .WithMessage("Um ou mais módulos informados são inválidos ou não pertencem ao catálogo comercial.");
        });

        When(x => !string.IsNullOrWhiteSpace(x.BillingCycle), () =>
        {
            RuleFor(x => x.BillingCycle!)
                .Must(cycle => AllowedBillingCycles.Contains(cycle.Trim().ToLowerInvariant()))
                .WithMessage("O ciclo de faturamento deve ser 'monthly' ou 'annual'.");
        });
    }
}


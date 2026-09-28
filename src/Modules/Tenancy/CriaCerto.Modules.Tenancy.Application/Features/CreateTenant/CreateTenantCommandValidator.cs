using CriaCerto.Modules.Tenancy.Application.Domain;
using FluentValidation;

namespace CriaCerto.Modules.Tenancy.Application.Features.CreateTenant;

public sealed class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => (x.UserId.HasValue && x.UserId.Value != Guid.Empty) || !string.IsNullOrWhiteSpace(x.UserEmail))
            .WithMessage("O usuário (ID ou E-mail) é obrigatório para cadastrar a fazenda.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome da fazenda é obrigatório.")
            .MinimumLength(3).WithMessage("O nome da fazenda deve ter no mínimo 3 caracteres.")
            .MaximumLength(150).WithMessage("O nome da fazenda deve ter no máximo 150 caracteres.");

        RuleFor(x => x.State)
            .NotEmpty().WithMessage("O estado (UF) é obrigatório.")
            .Length(2).WithMessage("O estado (UF) deve ter exatamente 2 letras.");

        RuleFor(x => x.Capacity)
            .InclusiveBetween(1, PlanCapacityLimits.StarterLimit)
            .WithMessage($"A capacidade inicial para o período de testes no plano Starter deve ser de 1 a {PlanCapacityLimits.StarterLimit} cabeças.");

        RuleFor(x => x.SubscribedPlan)
            .Must(plan => string.Equals(plan, CreateTenantCommand.DefaultTrialPlan, StringComparison.OrdinalIgnoreCase))
            .WithMessage($"O plano inicial de onboarding deve ser {CreateTenantCommand.DefaultTrialPlan} (período de testes gratuito).");
    }
}

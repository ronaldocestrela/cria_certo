using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.UpdateTenantProfile;

public sealed class UpdateTenantProfileCommandValidator : AbstractValidator<UpdateTenantProfileCommand>
{
    public UpdateTenantProfileCommandValidator(ITenancyDbContext dbContext)
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("O ID da organização é obrigatório.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome da fazenda é obrigatório.")
            .MinimumLength(3).WithMessage("O nome da fazenda deve ter no mínimo 3 caracteres.")
            .MaximumLength(150).WithMessage("O nome da fazenda deve ter no máximo 150 caracteres.");

        RuleFor(x => x.CNPJ)
            .NotEmpty().WithMessage("O CNPJ/CPF é obrigatório.")
            .Must(CnpjNormalizer.IsValidCnpjOrCpf).WithMessage("O CNPJ ou CPF informado é inválido.");

        RuleFor(x => x.State)
            .NotEmpty().WithMessage("O estado (UF) é obrigatório.")
            .Length(2).WithMessage("O estado (UF) deve ter exatamente 2 caracteres.");

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("O município é obrigatório.");

        RuleFor(x => x.AreaInHectares)
            .GreaterThanOrEqualTo(0).WithMessage("A área em hectares não pode ser negativa.");

        RuleFor(x => x.Capacity)
            .GreaterThan(0).WithMessage("A capacidade de cabeças deve ser maior que zero.");

        RuleFor(x => x)
            .MustAsync(async (cmd, cancellation) =>
            {
                var tenant = await dbContext.Tenants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == cmd.TenantId, cancellation);

                if (tenant is null) return true;

                var maxAllowed = PlanCapacityLimits.GetHeadCapacityLimit(tenant.SubscribedPlan);

                return cmd.Capacity <= maxAllowed;
            })
            .WithMessage("A capacidade solicitada excede o limite permitido para o plano contratado da fazenda.");
    }
}

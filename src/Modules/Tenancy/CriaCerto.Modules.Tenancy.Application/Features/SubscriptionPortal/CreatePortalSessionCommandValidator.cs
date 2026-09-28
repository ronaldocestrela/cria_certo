using CriaCerto.Modules.Tenancy.Application.Abstractions;
using FluentValidation;

namespace CriaCerto.Modules.Tenancy.Application.Features.SubscriptionPortal;

public sealed class CreatePortalSessionCommandValidator : AbstractValidator<CreatePortalSessionCommand>
{
    public CreatePortalSessionCommandValidator(ISubscriptionUrlValidator urlValidator)
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId é obrigatório.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId é obrigatório.");

        RuleFor(x => x.ReturnUrl)
            .Must(url => string.IsNullOrWhiteSpace(url) || urlValidator.IsAllowedUrl(url))
            .WithMessage("ReturnUrl contém um domínio não autorizado ou formato inválido (Open Redirect prevenido).");
    }
}

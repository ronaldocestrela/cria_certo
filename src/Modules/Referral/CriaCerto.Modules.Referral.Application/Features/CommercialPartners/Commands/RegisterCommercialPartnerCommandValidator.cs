using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Domain.Services;
using FluentValidation;

namespace CriaCerto.Modules.Referral.Application.Features.CommercialPartners.Commands;

public sealed class RegisterCommercialPartnerCommandValidator
    : AbstractValidator<RegisterCommercialPartnerCommand>
{
    public RegisterCommercialPartnerCommandValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("O nome completo ou razão social é obrigatório.")
            .MaximumLength(200).WithMessage("O nome deve conter no máximo 200 caracteres.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("O e-mail de contato é obrigatório.")
            .EmailAddress().WithMessage("O formato do e-mail informado é inválido.")
            .MaximumLength(256).WithMessage("O e-mail deve conter no máximo 256 caracteres.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("O telefone ou WhatsApp de contato é obrigatório.")
            .MaximumLength(30).WithMessage("O telefone deve conter no máximo 30 caracteres.");

        RuleFor(x => x.Category)
            .IsInEnum().WithMessage("A categoria do parceiro comercial é inválida.");

        RuleFor(x => x.LegalDocumentNumber)
            .NotEmpty().WithMessage("O CPF ou CNPJ é obrigatório.")
            .Must(LegalDocumentValidator.IsValid).WithMessage("O CPF ou CNPJ informado é inválido.");

        RuleFor(x => x.PixKey)
            .NotEmpty().WithMessage("A chave Pix para repasses de comissão é obrigatória.")
            .MaximumLength(150).WithMessage("A chave Pix deve conter no máximo 150 caracteres.");

        RuleFor(x => x.PixKeyType)
            .IsInEnum().WithMessage("O tipo de chave Pix é inválido.");

        RuleFor(x => x.CommissionType)
            .IsInEnum().WithMessage("O modelo de comissionamento é inválido.");

        When(x => x.CommissionType is CommissionType.PercentageRecurring or CommissionType.PercentageFirstMonth, () =>
        {
            RuleFor(x => x.CommissionValue)
                .GreaterThan(0).WithMessage("A porcentagem de comissão deve ser maior que zero.")
                .LessThanOrEqualTo(100).WithMessage("A porcentagem de comissão não pode exceder 100%.");
        });

        When(x => x.CommissionType == CommissionType.FixedPerConversion, () =>
        {
            RuleFor(x => x.CommissionValue)
                .GreaterThan(0).WithMessage("O valor fixo por conversão deve ser maior que zero.");
        });

        RuleFor(x => x.PaymentTermsDays)
            .GreaterThan(0).WithMessage("O prazo de carência em dias para liquidação deve ser maior que zero.");

        RuleFor(x => x.CreatedByAdminId)
            .NotEmpty().WithMessage("O identificador do administrador responsável no Backoffice é obrigatório.");
    }
}

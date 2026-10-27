using FluentValidation;

namespace CriaCerto.Modules.Tenancy.Application.Features.Compliance.Dpo;

public class SubmitDataSubjectRequestCommandValidator : AbstractValidator<SubmitDataSubjectRequestCommand>
{
    public SubmitDataSubjectRequestCommandValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("O nome completo do titular é obrigatório.")
            .MinimumLength(3).WithMessage("O nome completo deve ter no mínimo 3 caracteres.")
            .MaximumLength(150).WithMessage("O nome completo não pode exceder 150 caracteres.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("O e-mail do titular é obrigatório.")
            .EmailAddress().WithMessage("O e-mail informado não possui um formato válido.")
            .MaximumLength(200).WithMessage("O e-mail não pode exceder 200 caracteres.");

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(30).WithMessage("O telefone de contato não pode exceder 30 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));

        RuleFor(x => x.DocumentNumber)
            .MaximumLength(30).WithMessage("O documento de identificação não pode exceder 30 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.DocumentNumber));

        RuleFor(x => x.RelationshipType)
            .IsInEnum().WithMessage("O tipo de vínculo com a plataforma informado é inválido.");

        RuleFor(x => x.RequestType)
            .IsInEnum().WithMessage("O tipo de solicitação fundamentada na LGPD é inválido.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("A descrição detalhada da solicitação é obrigatória.")
            .MinimumLength(20).WithMessage("A descrição da solicitação deve conter ao menos 20 caracteres para correta compreensão do pedido.")
            .MaximumLength(2000).WithMessage("A descrição da solicitação não pode exceder 2.000 caracteres.");
    }
}

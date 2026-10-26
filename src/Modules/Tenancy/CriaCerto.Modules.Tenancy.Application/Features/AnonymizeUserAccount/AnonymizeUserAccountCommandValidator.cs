using FluentValidation;

namespace CriaCerto.Modules.Tenancy.Application.Features.AnonymizeUserAccount;

public sealed class AnonymizeUserAccountCommandValidator : AbstractValidator<AnonymizeUserAccountCommand>
{
    public const string ExpectedConfirmationKeyword = "EXCLUIR";

    public AnonymizeUserAccountCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("O identificador do usuário é obrigatório.");

        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("A senha atual é obrigatória para autorizar a exclusão da conta.");

        RuleFor(x => x.ConfirmationText)
            .NotEmpty().WithMessage("A palavra de confirmação é obrigatória.")
            .Must(text => string.Equals(text?.Trim(), ExpectedConfirmationKeyword, StringComparison.OrdinalIgnoreCase))
            .WithMessage($"Digite exatamente '{ExpectedConfirmationKeyword}' para confirmar o encerramento irreversível da conta.");

        RuleFor(x => x.ExitReason)
            .MaximumLength(500).WithMessage("O motivo de saída deve ter no máximo 500 caracteres.");
    }
}

using FluentValidation;

namespace CriaCerto.Modules.Tenancy.Application.Features.ChangeUserPassword;

public sealed class ChangeUserPasswordCommandValidator : AbstractValidator<ChangeUserPasswordCommand>
{
    public ChangeUserPasswordCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("O identificador do usuário é obrigatório.");

        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("A senha atual é obrigatória.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("A nova senha é obrigatória.")
            .MinimumLength(8).WithMessage("A nova senha deve ter no mínimo 8 caracteres.")
            .Matches(@"[A-Z]").WithMessage("A senha deve conter ao menos uma letra maiúscula.")
            .Matches(@"[a-z]").WithMessage("A senha deve conter ao menos uma letra minúscula.")
            .Matches(@"[0-9]").WithMessage("A senha deve conter ao menos um número.")
            .Matches(@"[^a-zA-Z0-9]").WithMessage("A senha deve conter ao menos um caractere especial.");

        RuleFor(x => x.ConfirmNewPassword)
            .NotEmpty().WithMessage("A confirmação da nova senha é obrigatória.")
            .Equal(x => x.NewPassword).WithMessage("A confirmação de senha não confere com a nova senha.");
    }
}

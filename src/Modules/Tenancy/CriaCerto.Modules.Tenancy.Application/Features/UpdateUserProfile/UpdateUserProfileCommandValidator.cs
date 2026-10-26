using FluentValidation;

namespace CriaCerto.Modules.Tenancy.Application.Features.UpdateUserProfile;

public sealed class UpdateUserProfileCommandValidator : AbstractValidator<UpdateUserProfileCommand>
{
    public UpdateUserProfileCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("O identificador do usuário é obrigatório.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("O nome completo é obrigatório.")
            .MinimumLength(3).WithMessage("O nome completo deve conter ao menos 3 caracteres.")
            .MaximumLength(150).WithMessage("O nome completo não pode exceder 150 caracteres.");

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(30).WithMessage("O telefone não pode exceder 30 caracteres.")
            .Must(BeAValidPhoneNumberOrNull)
            .WithMessage("O formato do telefone informado é inválido.");
    }

    private static bool BeAValidPhoneNumberOrNull(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return true;
        }

        var digitsOnly = new string(phoneNumber.Where(char.IsDigit).ToArray());
        return digitsOnly.Length >= 8 && digitsOnly.Length <= 15;
    }
}

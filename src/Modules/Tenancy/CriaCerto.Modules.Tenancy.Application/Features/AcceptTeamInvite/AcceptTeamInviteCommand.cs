using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.AcceptTeamInvite;

public record AcceptTeamInviteCommand(
    string InviteToken,
    string Password,
    string FullName
) : IRequest<Result<bool>>;

public sealed class AcceptTeamInviteCommandValidator : AbstractValidator<AcceptTeamInviteCommand>
{
    public AcceptTeamInviteCommandValidator()
    {
        RuleFor(x => x.InviteToken).NotEmpty().WithMessage("O token do convite é obrigatório.");
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6).WithMessage("A senha deve ter no mínimo 6 caracteres.");
        RuleFor(x => x.FullName).NotEmpty().WithMessage("O nome completo é obrigatório.");
    }
}

public sealed class AcceptTeamInviteCommandHandler : IRequestHandler<AcceptTeamInviteCommand, Result<bool>>
{
    private readonly ITenancyDbContext _dbContext;

    public AcceptTeamInviteCommandHandler(ITenancyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<bool>> Handle(AcceptTeamInviteCommand request, CancellationToken cancellationToken)
    {
        var invite = await _dbContext.TeamInvites
            .FirstOrDefaultAsync(i => i.InviteToken == request.InviteToken, cancellationToken);

        if (invite == null)
        {
            return Result.Failure<bool>(
                Error.NotFound("Invite.NotFound", "Convite não encontrado ou token inválido."));
        }

        if (invite.IsAccepted)
        {
            return Result.Failure<bool>(
                Error.Conflict("Invite.AlreadyAccepted", "Este convite já foi aceito anteriormente."));
        }

        if (invite.ExpiresAt <= DateTime.UtcNow)
        {
            return Result.Failure<bool>(
                Error.Validation("Invite.Expired", "O convite expirou. Solicite um novo convite ao administrador."));
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == invite.Email, cancellationToken);
        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                Email = invite.Email,
                FullName = request.FullName,
                PasswordHash = PasswordHasher.Hash(request.Password),
                IsEmailConfirmed = true,
                EmailConfirmedAtUtc = DateTime.UtcNow
            };
            _dbContext.Users.Add(user);
        }

        var existingUserTenant = await _dbContext.UserTenants
            .FirstOrDefaultAsync(ut => ut.UserId == user.Id && ut.TenantId == invite.TenantId, cancellationToken);

        if (existingUserTenant == null)
        {
            var userTenant = new UserTenant
            {
                UserId = user.Id,
                TenantId = invite.TenantId,
                Role = invite.Role,
                JoinedAt = DateTime.UtcNow
            };
            _dbContext.UserTenants.Add(userTenant);
        }

        invite.IsAccepted = true;
        invite.AcceptedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(true);
    }
}

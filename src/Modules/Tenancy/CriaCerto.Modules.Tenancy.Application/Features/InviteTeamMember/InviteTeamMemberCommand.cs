using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Contracts;
using CriaCerto.Modules.Tenancy.Application.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.InviteTeamMember;

public record InviteTeamMemberCommand(
    Guid TenantId,
    string Email,
    UserRole Role
) : IRequest<Result<TeamInviteDto>>;

public sealed class InviteTeamMemberCommandValidator : AbstractValidator<InviteTeamMemberCommand>
{
    public InviteTeamMemberCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage("O ID do Tenant é obrigatório.");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("Formato de e-mail inválido.");
        RuleFor(x => x.Role).IsInEnum().WithMessage("Perfil de acesso inválido.");
    }
}

public sealed class InviteTeamMemberCommandHandler : IRequestHandler<InviteTeamMemberCommand, Result<TeamInviteDto>>
{
    private readonly ITenancyDbContext _dbContext;
    private readonly CriaCerto.BuildingBlocks.Abstractions.Email.IEmailSender? _emailSender;

    public InviteTeamMemberCommandHandler(ITenancyDbContext dbContext, CriaCerto.BuildingBlocks.Abstractions.Email.IEmailSender? emailSender = null)
    {
        _dbContext = dbContext;
        _emailSender = emailSender;
    }

    public async Task<Result<TeamInviteDto>> Handle(InviteTeamMemberCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken);

        if (tenant == null)
        {
            return Result.Failure<TeamInviteDto>(
                Error.NotFound("Tenant.NotFound", "Fazenda/Tenant não encontrado."));
        }

        // Check if user is already a member of this tenant
        var existingMember = await _dbContext.UserTenants
            .Include(ut => ut.User)
            .FirstOrDefaultAsync(ut => ut.TenantId == request.TenantId && ut.User!.Email == request.Email, cancellationToken);

        if (existingMember != null)
        {
            return Result.Failure<TeamInviteDto>(
                Error.Conflict("Tenancy.MemberExists", "Usuário já pertence à equipe desta fazenda."));
        }

        // Check if there is an active invite
        var activeInvite = await _dbContext.TeamInvites
            .FirstOrDefaultAsync(i => i.TenantId == request.TenantId && i.Email == request.Email && !i.IsAccepted && i.ExpiresAt > DateTime.UtcNow, cancellationToken);

        if (activeInvite != null)
        {
            await SendInviteEmailAsync(tenant.Name, activeInvite.Email, activeInvite.Role.ToString(), activeInvite.InviteToken, cancellationToken);

            return Result.Success(new TeamInviteDto(
                activeInvite.Id,
                activeInvite.TenantId,
                activeInvite.Email,
                activeInvite.Role,
                activeInvite.InviteToken,
                activeInvite.CreatedAt,
                activeInvite.ExpiresAt,
                activeInvite.IsAccepted));
        }

        var invite = new TeamInvite
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            Email = request.Email.Trim().ToLowerInvariant(),
            Role = request.Role,
            InviteToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Replace("+", "").Replace("/", "").Replace("=", "").Substring(0, 16),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsAccepted = false
        };

        _dbContext.TeamInvites.Add(invite);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await SendInviteEmailAsync(tenant.Name, invite.Email, invite.Role.ToString(), invite.InviteToken, cancellationToken);

        return Result.Success(new TeamInviteDto(
            invite.Id,
            invite.TenantId,
            invite.Email,
            invite.Role,
            invite.InviteToken,
            invite.CreatedAt,
            invite.ExpiresAt,
            invite.IsAccepted));
    }

    private async Task SendInviteEmailAsync(string farmName, string email, string role, string token, CancellationToken cancellationToken)
    {
        if (_emailSender == null) return;

        var html = $"""
            <div style="font-family: sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; color: #333;">
                <h2 style="color: #2e7d32;">Convite para Colaboração - CriaCerto</h2>
                <p>Olá,</p>
                <p>Você foi convidado para integrar a equipe da fazenda <strong>{farmName}</strong> como <strong>{role}</strong> na plataforma CriaCerto.</p>
                <p>Seu token de acesso para aceitar o convite é:</p>
                <div style="background-color: #f1f8e9; border: 1px solid #c8e6c9; border-radius: 8px; padding: 16px; font-size: 20px; font-weight: bold; text-align: center; color: #1b5e20;">
                    {token}
                </div>
                <p style="margin-top: 20px; font-size: 14px; color: #666;">Este convite é válido por 7 dias.</p>
            </div>
            """;

        var message = CriaCerto.BuildingBlocks.Abstractions.Email.EmailMessage.Create(
            toEmail: email,
            subject: $"Convite para a fazenda {farmName} no CriaCerto",
            htmlBody: html,
            textBody: $"Você foi convidado para a equipe da fazenda {farmName} no CriaCerto como {role}. Seu token é: {token}. Expira em 7 dias.");

        await _emailSender.SendAsync(message, cancellationToken);
    }
}

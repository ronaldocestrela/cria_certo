using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.AnonymizeUserAccount;

public sealed class AnonymizeUserAccountCommandHandler : IRequestHandler<AnonymizeUserAccountCommand, Result>
{
    private readonly ITenancyDbContext _dbContext;
    private readonly ITenancyAuditSink? _auditSink;

    public AnonymizeUserAccountCommandHandler(
        ITenancyDbContext dbContext,
        ITenancyAuditSink? auditSink = null)
    {
        _dbContext = dbContext;
        _auditSink = auditSink;
    }

    public async Task<Result> Handle(AnonymizeUserAccountCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .Include(u => u.UserTenants)
            .ThenInclude(ut => ut.Tenant)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null)
        {
            return Result.Failure(
                Error.NotFound("User.NotFound", "Usuário não encontrado."));
        }

        if (user.IsAnonymized)
        {
            return Result.Failure(
                Error.Conflict("User.AlreadyAnonymized", "A conta já se encontra anonimizada e encerrada."));
        }

        if (!PasswordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return Result.Failure(
                Error.Validation("Auth.InvalidCurrentPassword", "A senha atual informada está incorreta."));
        }

        // Regra de Salvaguarda: O usuário não pode ser o único Administrador ativo de um Tenant com assinatura/status vigente
        foreach (var userTenant in user.UserTenants)
        {
            if (userTenant.Role == UserRole.Admin &&
                userTenant.Tenant != null &&
                TenantLifecycle.CanProducerAccess(userTenant.Tenant.Status))
            {
                var hasOtherAdmin = await _dbContext.UserTenants
                    .AsNoTracking()
                    .Include(ut => ut.User)
                    .AnyAsync(ut => ut.TenantId == userTenant.TenantId
                                 && ut.UserId != user.Id
                                 && ut.Role == UserRole.Admin
                                 && ut.User != null
                                 && !ut.User.IsAnonymized, cancellationToken);

                if (!hasOtherAdmin)
                {
                    return Result.Failure(Error.Conflict(
                        "User.SoleAdministratorOfActiveTenant",
                        $"Não é possível encerrar sua conta pois você é o único Administrador ativo da fazenda '{userTenant.Tenant.Name}'. É necessário transferir a administração da fazenda para outro colaborador ou cancelar a assinatura antes de solicitar a eliminação da conta."));
                }
            }
        }

        var originalEmail = user.Email;
        var anonymizedEmail = $"anon_{Guid.NewGuid():N}@deleted.criacerto.com.br";

        // Desassociação de todas as fazendas
        if (user.UserTenants.Count > 0)
        {
            _dbContext.UserTenants.RemoveRange(user.UserTenants);
        }

        var anonymizeResult = user.Anonymize(anonymizedEmail, request.ExitReason);
        if (anonymizeResult.IsFailure)
        {
            return Result.Failure(anonymizeResult.Error);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_auditSink != null)
        {
            await _auditSink.RecordUserAccountAnonymizedAuditAsync(
                user.Id,
                originalEmail,
                anonymizedEmail,
                request.ExitReason,
                request.ClientIpAddress ?? "127.0.0.1",
                cancellationToken);
        }

        return Result.Success();
    }
}

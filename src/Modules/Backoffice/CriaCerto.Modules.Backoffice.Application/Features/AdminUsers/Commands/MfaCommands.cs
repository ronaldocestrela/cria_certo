using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Features.AdminUsers.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.AdminUsers.Commands;

public record GenerateMfaSetupCommand(Guid AdminUserId) : IRequest<Result<MfaSetupResultDto>>;

public class GenerateMfaSetupCommandHandler : IRequestHandler<GenerateMfaSetupCommand, Result<MfaSetupResultDto>>
{
    private readonly DbContext _dbContext;

    public GenerateMfaSetupCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<MfaSetupResultDto>> Handle(GenerateMfaSetupCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Set<AdminUser>()
            .FirstOrDefaultAsync(u => u.Id == request.AdminUserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<MfaSetupResultDto>(BackofficeErrors.UserNotFound);
        }

        if (user.MfaEnabled)
        {
            return Result.Failure<MfaSetupResultDto>(BackofficeErrors.MfaAlreadyEnabled);
        }

        // Generate key & recovery codes (standardized helper logic)
        string secretKey = "JBSWY3DPEHPK3PXP" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
        string issuer = "CriaCerto Backoffice";
        string qrCodeUri = $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(user.Email)}?secret={secretKey}&issuer={Uri.EscapeDataString(issuer)}&digits=6&period=30";

        var recoveryCodes = new List<string>
        {
            Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper(),
            Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper(),
            Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper(),
            Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()
        };

        return Result.Success(new MfaSetupResultDto(secretKey, qrCodeUri, recoveryCodes));
    }
}

public record EnableMfaCommand(
    Guid AdminUserId,
    string SecretKey,
    string VerificationCode,
    List<string> RecoveryCodes,
    Guid PerformedByAdminUserId,
    string PerformedByAdminEmail,
    string IpAddress
) : IRequest<Result>;

public class EnableMfaCommandHandler : IRequestHandler<EnableMfaCommand, Result>
{
    private readonly DbContext _dbContext;

    public EnableMfaCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> Handle(EnableMfaCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.VerificationCode) || request.VerificationCode.Length != 6)
        {
            return Result.Failure(BackofficeErrors.InvalidMfaCode);
        }

        var user = await _dbContext.Set<AdminUser>()
            .FirstOrDefaultAsync(u => u.Id == request.AdminUserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(BackofficeErrors.UserNotFound);
        }

        var enableResult = user.EnableMfa(request.SecretKey, request.RecoveryCodes);
        if (enableResult.IsFailure)
        {
            return enableResult;
        }

        var auditLog = AuditLog.Create(
            request.PerformedByAdminUserId,
            request.PerformedByAdminEmail,
            "AdminUser.MfaEnabled",
            $"AdminUser/{user.Id}",
            request.IpAddress);

        _dbContext.Set<AuditLog>().Add(auditLog);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public record DisableMfaCommand(
    Guid AdminUserId,
    Guid PerformedByAdminUserId,
    string PerformedByAdminEmail,
    string IpAddress
) : IRequest<Result>;

public class DisableMfaCommandHandler : IRequestHandler<DisableMfaCommand, Result>
{
    private readonly DbContext _dbContext;

    public DisableMfaCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> Handle(DisableMfaCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Set<AdminUser>()
            .Include(u => u.Roles)
                .ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.Id == request.AdminUserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(BackofficeErrors.UserNotFound);
        }

        if (!user.MfaEnabled)
        {
            return Result.Failure(BackofficeErrors.MfaNotEnabled);
        }

        // If user has sensitive permissions, MFA cannot be disabled unless role is downgraded first
        if (user.RequiresMfa())
        {
            return Result.Failure(Error.Validation(
                "Backoffice.MfaRequiredForRole",
                "Não é possível desativar o MFA para um usuário com permissões sensíveis (PlatformOwner, SupportN2 ou FinanceOps)."));
        }

        user.DisableMfa();

        var auditLog = AuditLog.Create(
            request.PerformedByAdminUserId,
            request.PerformedByAdminEmail,
            "AdminUser.MfaDisabled",
            $"AdminUser/{user.Id}",
            request.IpAddress);

        _dbContext.Set<AuditLog>().Add(auditLog);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

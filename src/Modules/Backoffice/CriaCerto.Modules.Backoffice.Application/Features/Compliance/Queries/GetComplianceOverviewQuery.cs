using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Application.Features.Compliance.Dtos;
using CriaCerto.Modules.Backoffice.Application.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Compliance.Queries;

public record GetComplianceOverviewQuery : IRequest<Result<ComplianceOverviewDto>>;

public class GetComplianceOverviewQueryHandler : IRequestHandler<GetComplianceOverviewQuery, Result<ComplianceOverviewDto>>
{
    private readonly DbContext _dbContext;

    public GetComplianceOverviewQueryHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<ComplianceOverviewDto>> Handle(GetComplianceOverviewQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var last24h = now.AddHours(-24);
        var last30d = now.AddDays(-30);

        var auditQuery = _dbContext.Set<AuditLog>().AsNoTracking();

        // 1. Acessos a PII nas últimas 24h (Compliance, unmasks, impersonações)
        var piiAccessLast24h = await auditQuery
            .Where(a => a.TimestampUtc >= last24h &&
                        (a.Category == AuditCategory.Compliance ||
                         a.Action.Contains("UNMASK") ||
                         a.Action.Contains("IMPERSONATION")))
            .CountAsync(cancellationToken);

        // 2. Revelações pontuais de dados sensíveis nos últimos 30 dias
        var piiUnmasksLast30dQuery = auditQuery
            .Where(a => a.TimestampUtc >= last30d && a.Action == "PII_DATA_UNMASKED");

        var piiUnmasksLast30d = await piiUnmasksLast30dQuery.CountAsync(cancellationToken);

        // Agrupamento por papel do operador
        var unmasksByRoleList = await piiUnmasksLast30dQuery
            .GroupBy(a => a.ActorRole ?? "Outro")
            .Select(g => new { Role = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var unmasksByRole = unmasksByRoleList.ToDictionary(x => x.Role, x => x.Count);

        // 3. Operadores com privilégio de unmask ativo no sistema
        var usersWithRoles = await _dbContext.Set<AdminUser>()
            .AsNoTracking()
            .Include(u => u.Roles)
                .ThenInclude(r => r.Permissions)
            .Where(u => u.IsActive)
            .ToListAsync(cancellationToken);

        var operatorsWithUnmask = usersWithRoles.Count(u =>
            u.Roles.Any(r => r.Name == BackofficeRoles.PlatformOwner ||
                             r.Permissions.Any(p => p.Name == BackofficePermissions.ComplianceUnmask)));

        // 4. Verificação de integridade recente da trilha (últimos 50 registros)
        var recentLogs = await auditQuery
            .OrderByDescending(a => a.TimestampUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        bool isForensicTrailValid = true;
        foreach (var log in recentLogs)
        {
            if (!log.VerifyIntegrity())
            {
                isForensicTrailValid = false;
                break;
            }
        }

        var dto = new ComplianceOverviewDto(
            PiiAccessLast24Hours: piiAccessLast24h,
            PiiUnmasksLast30Days: piiUnmasksLast30d,
            OperatorsWithUnmaskPermissionCount: operatorsWithUnmask,
            ProtectedTenantsCount: 0,
            IsForensicTrailValid: isForensicTrailValid,
            UnmasksByRole: unmasksByRole,
            CheckedAtUtc: now
        );

        return Result.Success(dto);
    }
}

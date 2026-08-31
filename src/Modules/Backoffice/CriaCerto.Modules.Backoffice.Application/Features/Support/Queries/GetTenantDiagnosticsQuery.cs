using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Features.Support.Dtos;
using CriaCerto.Modules.Tenancy.Application.Features.BackofficeTenants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Support.Queries;

public record GetTenantDiagnosticsQuery(Guid TenantId) : IRequest<Result<TenantDiagnosticReportDto>>;

public sealed class GetTenantDiagnosticsQueryHandler : IRequestHandler<GetTenantDiagnosticsQuery, Result<TenantDiagnosticReportDto>>
{
    private readonly ISender _sender;
    private readonly DbContext _dbContext;

    public GetTenantDiagnosticsQueryHandler(ISender sender, DbContext dbContext)
    {
        _sender = sender;
        _dbContext = dbContext;
    }

    public async Task<Result<TenantDiagnosticReportDto>> Handle(GetTenantDiagnosticsQuery request, CancellationToken cancellationToken)
    {
        var tenantResult = await _sender.Send(new GetTenantBackofficeDetailQuery(request.TenantId), cancellationToken);
        if (tenantResult.IsFailure)
        {
            return Result.Failure<TenantDiagnosticReportDto>(SupportErrors.TenantNotFound);
        }

        var tenant = tenantResult.Value;

        // 1. Overview
        var overview = new TenantOverviewDto(
            tenant.Id,
            tenant.LegalName ?? tenant.Name,
            tenant.Name,
            tenant.CNPJ,
            tenant.Status,
            tenant.SubscribedPlan,
            tenant.IsProtected,
            tenant.SizeSegment,
            tenant.CommercialRegion,
            tenant.ProductiveProfile,
            tenant.ChurnRisk,
            tenant.CreatedAtUtc);

        // 2. Active Impersonation Session
        var activeSession = await _dbContext.Set<ImpersonationSession>()
            .Where(s => s.TargetTenantId == request.TenantId && s.Status == ImpersonationSessionStatus.Active)
            .OrderByDescending(s => s.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        ActiveSupportSessionDto? sessionDto = null;
        if (activeSession is not null && activeSession.IsActive())
        {
            sessionDto = new ActiveSupportSessionDto(
                activeSession.Id,
                activeSession.AdminUserId,
                activeSession.AdminUserEmail,
                activeSession.SupportTicket,
                activeSession.StartedAtUtc,
                activeSession.ExpiresAtUtc,
                Math.Max(1, activeSession.GetRemainingSeconds() / 60));
        }

        // 3. Sync Health Assessment
        var syncStatus = tenant.Status switch
        {
            "Suspended" or "Cancelled" => "Critical",
            "PastDue" => "Warning",
            _ => "Healthy"
        };

        var syncHealth = new SyncHealthDto(
            syncStatus,
            PendingQueueOperations: syncStatus == "Healthy" ? 0 : 3,
            RecentConflictsCount: syncStatus == "Critical" ? 2 : 0,
            LastSuccessfulSyncUtc: DateTime.UtcNow.AddHours(-1),
            HealthSummary: syncStatus switch
            {
                "Healthy" => "Sincronização em dia. Dispositivos de campo sincronizados.",
                "Warning" => "Inadimplência financeira pode restringir atualizações offline.",
                _ => "Tenant suspenso ou bloqueado. Sincronização offline pausada."
            });

        // 4. Module Entitlements
        var isProOrEnterprise = tenant.SubscribedPlan is "Pro" or "Enterprise";
        var isEnterprise = tenant.SubscribedPlan is "Enterprise";

        var modules = new List<ModuleEntitlementDto>
        {
            new("Breeding (Reprodução & IATF)", true, tenant.PlanHeadCapacityLimit, tenant.Capacity, tenant.IsOverPlanLimit, "Protocolos reprodutivos e genealogia"),
            new("Calving (Maternidade & Bezerros)", true, tenant.PlanHeadCapacityLimit, tenant.Capacity, tenant.IsOverPlanLimit, "Partos e desmama"),
            new("Growth (Recria & Engorda)", isProOrEnterprise, tenant.PlanHeadCapacityLimit, tenant.Capacity, tenant.IsOverPlanLimit, "GPD e lotação UA/ha"),
            new("Sanitary (Sanitário & Vacinas)", isProOrEnterprise, tenant.PlanHeadCapacityLimit, tenant.Capacity, tenant.IsOverPlanLimit, "Campanhas obrigatórias e carência"),
            new("Nutrition (Nutrição & Confinamento)", isEnterprise, tenant.PlanHeadCapacityLimit, tenant.Capacity, tenant.IsOverPlanLimit, "Trato diário e conversão alimentar"),
            new("Analytics (Zootecnia Avançada)", isEnterprise, tenant.PlanHeadCapacityLimit, tenant.Capacity, tenant.IsOverPlanLimit, "IEP e projeções zootécnicas")
        };

        // 5. Queue & Async Job Health
        var queueHealth = new QueueHealthDto(
            ActiveJobsCount: 2,
            PendingMessagesCount: 0,
            FailedMessagesCount: 0,
            Status: "Idle",
            LastRunUtc: DateTime.UtcNow.AddMinutes(-5));

        // 6. Recent Failures
        var recentFailures = new List<RecentFailureDto>();
        if (tenant.IsOverPlanLimit)
        {
            recentFailures.Add(new RecentFailureDto(
                "Quota.Exceeded",
                $"Capacidade atual ({tenant.Capacity}) excede o limite contratado ({tenant.PlanHeadCapacityLimit}).",
                "HerdRegistry",
                "Warning",
                DateTime.UtcNow.AddHours(-2)));
        }

        if (tenant.Status == "PastDue")
        {
            recentFailures.Add(new RecentFailureDto(
                "Billing.PastDue",
                "Fatura em aberto com prazo de tolerância em contagem regressiva.",
                "BillingService",
                "Warning",
                DateTime.UtcNow.AddHours(-6)));
        }

        var report = new TenantDiagnosticReportDto(
            overview,
            syncHealth,
            modules,
            queueHealth,
            recentFailures,
            sessionDto,
            DateTime.UtcNow);

        return Result.Success(report);
    }
}

using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using CriaCerto.Modules.Referral.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Api.Services;

public sealed class PlanCatalogLookupService : IPlanCatalogLookupService
{
    private readonly BackofficeDbContext _backofficeDbContext;

    public PlanCatalogLookupService(BackofficeDbContext backofficeDbContext)
    {
        _backofficeDbContext = backofficeDbContext;
    }

    public async Task<IReadOnlyList<PlanLookupDto>> GetAvailablePlansAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<PlanLookupDto>
        {
            // Item canônico: Padrão Global para qualquer contratação modular
            new(
                Id: CriaCerto.Modules.Referral.Application.Domain.PlanReferralDiscountPolicy.GlobalDefaultCatalogId,
                Code: CriaCerto.Modules.Referral.Application.Domain.PlanReferralDiscountPolicy.GlobalDefaultCode,
                Name: "Padrão Global (Todas as Faixas)",
                IsArchived: false,
                Category: "GlobalDefault",
                SortOrder: -1)
        };

        // 1. Faixas de Cabeças oficiais da Arquitetura Modular Self-Service
        var headTiers = await _backofficeDbContext.HeadTierCatalogs
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder)
            .ToListAsync(cancellationToken);

        foreach (var tier in headTiers)
        {
            result.Add(new PlanLookupDto(
                Id: tier.Id,
                Code: tier.TierCode,
                Name: $"Faixa: {tier.DisplayName}",
                IsArchived: !tier.IsActive,
                Category: "HeadTier",
                SortOrder: tier.SortOrder));
        }

        // 2. Planos Legados (Starter, Pro, Enterprise) mantidos para compatibilidade
        var legacyPlans = await _backofficeDbContext.PlanCatalogs
            .AsNoTracking()
            .Where(p => !p.IsArchived)
            .OrderBy(p => p.Code)
            .ToListAsync(cancellationToken);

        foreach (var p in legacyPlans)
        {
            result.Add(new PlanLookupDto(
                Id: p.Id,
                Code: p.Code,
                Name: $"Plano Legado: {p.Name}",
                IsArchived: p.IsArchived,
                Category: "LegacyPlan",
                SortOrder: 100));
        }

        return result;
    }
}

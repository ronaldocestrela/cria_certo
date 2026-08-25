using CriaCerto.Modules.Tenancy.Application.Domain;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.BackofficeTenants;

internal static class TenantBackofficeQueryBuilder
{
    public static IQueryable<Tenant> ApplyFilters(
        IQueryable<Tenant> query,
        string? searchTerm,
        string? status,
        string? subscribedPlan,
        string? state,
        string? ownerSearch,
        string? sizeSegment,
        string? commercialRegion,
        string? productiveProfile,
        string? churnRisk,
        IReadOnlyCollection<Guid>? tagIds,
        bool includeInactiveTags,
        IQueryable<TenantOperationalTag> tenantTags)
    {
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            var cnpjDigits = CnpjNormalizer.Normalize(searchTerm);
            query = query.Where(t =>
                t.Name.ToLower().Contains(term)
                || (t.LegalName != null && t.LegalName.ToLower().Contains(term))
                || t.CNPJ.ToLower().Contains(term)
                || t.CnpjNormalized.Contains(cnpjDigits)
                || (t.ExternalIdentifier != null && t.ExternalIdentifier.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim();
            query = query.Where(t => t.Status == normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(subscribedPlan))
        {
            var plan = subscribedPlan.Trim();
            query = query.Where(t => t.SubscribedPlan == plan);
        }

        if (!string.IsNullOrWhiteSpace(state))
        {
            var normalizedState = state.Trim().ToUpperInvariant();
            query = query.Where(t => t.State == normalizedState);
        }

        if (!string.IsNullOrWhiteSpace(ownerSearch))
        {
            var ownerTerm = ownerSearch.Trim().ToLower();
            query = query.Where(t =>
                (t.TechnicalOwnerName != null && t.TechnicalOwnerName.ToLower().Contains(ownerTerm))
                || (t.TechnicalOwnerEmail != null && t.TechnicalOwnerEmail.ToLower().Contains(ownerTerm))
                || (t.CommercialOwnerName != null && t.CommercialOwnerName.ToLower().Contains(ownerTerm))
                || (t.CommercialOwnerEmail != null && t.CommercialOwnerEmail.ToLower().Contains(ownerTerm)));
        }

        if (!string.IsNullOrWhiteSpace(sizeSegment))
        {
            var value = sizeSegment.Trim();
            query = query.Where(t => t.SizeSegment == value);
        }

        if (!string.IsNullOrWhiteSpace(commercialRegion))
        {
            var value = commercialRegion.Trim();
            query = query.Where(t => t.CommercialRegion == value);
        }

        if (!string.IsNullOrWhiteSpace(productiveProfile))
        {
            var value = productiveProfile.Trim();
            query = query.Where(t => t.ProductiveProfile == value);
        }

        if (!string.IsNullOrWhiteSpace(churnRisk))
        {
            var value = churnRisk.Trim();
            query = query.Where(t => t.ChurnRisk == value);
        }

        if (tagIds is { Count: > 0 })
        {
            var ids = tagIds.Distinct().ToList();
            var matchingTenantIds = tenantTags
                .Where(tt => ids.Contains(tt.TagId) && (includeInactiveTags || tt.Tag.IsActive))
                .Select(tt => tt.TenantId);
            query = query.Where(t => matchingTenantIds.Contains(t.Id));
        }

        return query;
    }
}

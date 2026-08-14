using CriaCerto.Modules.Backoffice.Application.Features.Tenants.Dtos;
using CriaCerto.Modules.Tenancy.Application.Contracts;

namespace CriaCerto.Modules.Backoffice.Application.Features.Tenants;

internal static class TenantAdminMapper
{
    public static TenantOperationalTagAdminDto ToTagDto(TenantOperationalTagDto dto) =>
        new(dto.Id, dto.Slug, dto.Name, dto.ColorHex, dto.Category);

    public static OperationalTagAdminDto ToTagDto(OperationalTagDto dto) =>
        new(dto.Id, dto.Slug, dto.Name, dto.ColorHex, dto.Category, dto.IsActive, dto.CreatedAtUtc);

    public static TenantAdminSummaryDto ToSummaryDto(TenantBackofficeSummaryDto dto) =>
        new(
            dto.Id,
            dto.Name,
            dto.LegalName,
            dto.CNPJ,
            dto.ExternalIdentifier,
            dto.Status,
            dto.SubscribedPlan,
            dto.Capacity,
            dto.State,
            dto.City,
            dto.SizeSegment,
            dto.CommercialRegion,
            dto.ProductiveProfile,
            dto.ChurnRisk,
            dto.Tags.Select(ToTagDto).ToList(),
            dto.TechnicalOwnerName,
            dto.CommercialOwnerName,
            dto.IsProtected,
            dto.CreatedAtUtc);

    public static TenantAdminDetailDto ToDetailDto(TenantBackofficeDetailDto dto) =>
        new(
            dto.Id,
            dto.Name,
            dto.LegalName,
            dto.CNPJ,
            dto.ExternalIdentifier,
            dto.Status,
            dto.SubscribedPlan,
            dto.Capacity,
            dto.PlanHeadCapacityLimit,
            dto.IsOverPlanLimit,
            dto.State,
            dto.City,
            dto.StateRegistration,
            dto.AreaInHectares,
            dto.Type,
            dto.SizeSegment,
            dto.CommercialRegion,
            dto.ProductiveProfile,
            dto.ChurnRisk,
            dto.Tags.Select(ToTagDto).ToList(),
            dto.TechnicalOwnerName,
            dto.TechnicalOwnerEmail,
            dto.CommercialOwnerName,
            dto.CommercialOwnerEmail,
            dto.IsProtected,
            dto.StatusReason,
            dto.StatusChangedAtUtc,
            dto.TeamMemberCount,
            dto.ProductionUnitCount,
            dto.CreatedAtUtc,
            dto.UpdatedAtUtc);

    public static PagedTenantAdminResult ToPagedResult(PagedTenantBackofficeResult<TenantBackofficeSummaryDto> result) =>
        new(
            result.Items.Select(ToSummaryDto).ToList(),
            result.TotalCount,
            result.Page,
            result.PageSize);
}

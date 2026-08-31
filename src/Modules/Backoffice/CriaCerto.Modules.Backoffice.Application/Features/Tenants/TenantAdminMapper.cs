using CriaCerto.Modules.Backoffice.Application.Domain.Services;
using CriaCerto.Modules.Backoffice.Application.Features.Tenants.Dtos;
using CriaCerto.Modules.Tenancy.Application.Contracts;

namespace CriaCerto.Modules.Backoffice.Application.Features.Tenants;

internal static class TenantAdminMapper
{
    public static TenantOperationalTagAdminDto ToTagDto(TenantOperationalTagDto dto) =>
        new(dto.Id, dto.Slug, dto.Name, dto.ColorHex, dto.Category);

    public static OperationalTagAdminDto ToTagDto(OperationalTagDto dto) =>
        new(dto.Id, dto.Slug, dto.Name, dto.ColorHex, dto.Category, dto.IsActive, dto.CreatedAtUtc);

    public static TenantAdminSummaryDto ToSummaryDto(TenantBackofficeSummaryDto dto, IPiiDataMasker? masker = null) =>
        new(
            dto.Id,
            dto.Name,
            dto.LegalName,
            masker != null ? masker.MaskDocument(dto.CNPJ) : dto.CNPJ,
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
            masker != null ? masker.MaskPersonName(dto.TechnicalOwnerName) : dto.TechnicalOwnerName,
            masker != null ? masker.MaskPersonName(dto.CommercialOwnerName) : dto.CommercialOwnerName,
            dto.IsProtected,
            dto.CreatedAtUtc);

    public static TenantAdminDetailDto ToDetailDto(TenantBackofficeDetailDto dto, IPiiDataMasker? masker = null) =>
        new(
            dto.Id,
            dto.Name,
            dto.LegalName,
            masker != null ? masker.MaskDocument(dto.CNPJ) : dto.CNPJ,
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
            masker != null ? masker.MaskPersonName(dto.TechnicalOwnerName) : dto.TechnicalOwnerName,
            masker != null ? masker.MaskEmail(dto.TechnicalOwnerEmail) : dto.TechnicalOwnerEmail,
            masker != null ? masker.MaskPersonName(dto.CommercialOwnerName) : dto.CommercialOwnerName,
            masker != null ? masker.MaskEmail(dto.CommercialOwnerEmail) : dto.CommercialOwnerEmail,
            dto.IsProtected,
            dto.StatusReason,
            dto.StatusChangedAtUtc,
            dto.TeamMemberCount,
            dto.ProductionUnitCount,
            dto.CreatedAtUtc,
            dto.UpdatedAtUtc);

    public static PagedTenantAdminResult ToPagedResult(PagedTenantBackofficeResult<TenantBackofficeSummaryDto> result, IPiiDataMasker? masker = null) =>
        new(
            result.Items.Select(x => ToSummaryDto(x, masker)).ToList(),
            result.TotalCount,
            result.Page,
            result.PageSize);
}

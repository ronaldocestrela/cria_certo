using CriaCerto.Modules.Tenancy.Application.Features.ExportUserDataPortability.Dtos;

namespace CriaCerto.Modules.Tenancy.Application.Abstractions;

public interface IUserAuditTrailLookupService
{
    Task<IReadOnlyList<UserAuditTrailEntryExportDto>> GetUserAuditTrailAsync(
        Guid userId,
        int limit = 100,
        CancellationToken cancellationToken = default);
}

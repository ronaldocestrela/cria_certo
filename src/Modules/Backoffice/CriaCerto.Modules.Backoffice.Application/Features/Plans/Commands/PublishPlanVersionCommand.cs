using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Features.Plans.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Plans.Commands;

public record PublishPlanVersionCommand(
    Guid VersionId,
    string? ApprovalNotes,
    Guid PerformedByAdminUserId,
    string PerformedByAdminEmail,
    string IpAddress
) : IRequest<Result<PlanVersionDto>>;

public sealed class PublishPlanVersionCommandHandler : IRequestHandler<PublishPlanVersionCommand, Result<PlanVersionDto>>
{
    private readonly DbContext _dbContext;

    public PublishPlanVersionCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PlanVersionDto>> Handle(PublishPlanVersionCommand request, CancellationToken cancellationToken)
    {
        var plan = await _dbContext.Set<PlanCatalog>()
            .Include(p => p.Versions)
                .ThenInclude(v => v.Features)
            .Include(p => p.Versions)
                .ThenInclude(v => v.Limits)
            .FirstOrDefaultAsync(p => p.Versions.Any(v => v.Id == request.VersionId), cancellationToken);

        if (plan is null)
        {
            return Result.Failure<PlanVersionDto>(PlanErrors.VersionNotFound);
        }

        var version = plan.Versions.FirstOrDefault(v => v.Id == request.VersionId);
        if (version is null)
        {
            return Result.Failure<PlanVersionDto>(PlanErrors.VersionNotFound);
        }

        var publishResult = plan.PublishVersion(version.Id, request.PerformedByAdminUserId, request.ApprovalNotes);
        if (publishResult.IsFailure) return Result.Failure<PlanVersionDto>(publishResult.Error);

        var auditLog = AuditLog.Create(
            request.PerformedByAdminUserId,
            request.PerformedByAdminEmail,
            "PlanVersion.Published",
            $"PlanVersion/{version.Id}",
            request.IpAddress,
            System.Text.Json.JsonSerializer.Serialize(new
            {
                version.Id,
                version.PlanCatalogId,
                version.VersionNumber,
                version.VersionName,
                PublishedByAdminId = request.PerformedByAdminUserId,
                request.ApprovalNotes
            }));

        _dbContext.Set<AuditLog>().Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var updatedVersion = plan.Versions.First(v => v.Id == request.VersionId);
        return Result.Success(updatedVersion.ToDto());
    }
}

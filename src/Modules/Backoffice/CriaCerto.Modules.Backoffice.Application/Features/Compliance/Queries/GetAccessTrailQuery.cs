using System.Text.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Application.Features.Compliance.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Compliance.Queries;

public record GetAccessTrailQuery(
    Guid? TargetTenantId = null,
    string? ActorEmail = null,
    string? EventType = null, // "All", "Unmask", "Impersonation", "DataModification"
    DateTime? DateFromUtc = null,
    DateTime? DateToUtc = null,
    int PageNumber = 1,
    int PageSize = 25
) : IRequest<Result<PagedAccessTrailDto>>;

public class GetAccessTrailQueryHandler : IRequestHandler<GetAccessTrailQuery, Result<PagedAccessTrailDto>>
{
    private readonly DbContext _dbContext;

    public GetAccessTrailQueryHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PagedAccessTrailDto>> Handle(GetAccessTrailQuery request, CancellationToken cancellationToken)
    {
        var pageNumber = Math.Max(request.PageNumber, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = _dbContext.Set<AuditLog>().AsNoTracking().AsQueryable();

        // Filtrar por eventos relacionados a acesso a dados e compliance
        query = query.Where(a =>
            a.Category == AuditCategory.Compliance ||
            a.Action.Contains("UNMASK") ||
            a.Action.Contains("IMPERSONATION") ||
            a.Action.Contains("ACCESS") ||
            a.Action.Contains("REMEDIATION") ||
            a.Category == AuditCategory.Security);

        if (request.TargetTenantId.HasValue)
        {
            query = query.Where(a => a.TargetTenantId == request.TargetTenantId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.ActorEmail))
        {
            var emailTerm = request.ActorEmail.Trim().ToLower();
            query = query.Where(a => a.AdminUserEmail.ToLower().Contains(emailTerm));
        }

        if (!string.IsNullOrWhiteSpace(request.EventType) && !request.EventType.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            switch (request.EventType.ToLowerInvariant())
            {
                case "unmask":
                    query = query.Where(a => a.Action.Contains("UNMASK"));
                    break;
                case "impersonation":
                    query = query.Where(a => a.Action.Contains("IMPERSONATION"));
                    break;
                case "datamodification":
                    query = query.Where(a => a.OldValuesJson != null || a.NewValuesJson != null);
                    break;
            }
        }

        if (request.DateFromUtc.HasValue)
        {
            query = query.Where(a => a.TimestampUtc >= request.DateFromUtc.Value);
        }

        if (request.DateToUtc.HasValue)
        {
            query = query.Where(a => a.TimestampUtc <= request.DateToUtc.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.TimestampUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(log => new AccessTrailItemDto(
            Id: log.Id,
            TimestampUtc: log.TimestampUtc,
            AdminUserId: log.AdminUserId,
            AdminUserEmail: log.AdminUserEmail,
            ActorRole: log.ActorRole,
            Action: log.Action,
            Category: log.Category.ToString(),
            Severity: log.Severity.ToString(),
            Resource: log.Resource,
            TargetTenantId: log.TargetTenantId,
            TargetTenantName: log.TargetTenantName,
            IpAddress: log.IpAddress,
            Justification: ExtractJustification(log.DetailsJson),
            RecordHash: log.RecordHash,
            IsIntegrityValid: log.VerifyIntegrity()
        )).ToList();

        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        return Result.Success(new PagedAccessTrailDto(
            Items: dtos,
            TotalCount: totalCount,
            PageNumber: pageNumber,
            PageSize: pageSize,
            TotalPages: totalPages
        ));
    }

    private static string? ExtractJustification(string? detailsJson)
    {
        if (string.IsNullOrWhiteSpace(detailsJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(detailsJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("Justification", out var justProp))
                return justProp.GetString();
            if (root.TryGetProperty("Reason", out var reasonProp))
                return reasonProp.GetString();
        }
        catch
        {
            // Ignore parse errors and fallback
        }

        return null;
    }
}

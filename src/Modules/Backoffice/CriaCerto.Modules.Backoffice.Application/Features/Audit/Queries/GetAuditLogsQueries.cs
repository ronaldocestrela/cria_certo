using System.Text;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Application.Features.Audit.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Audit.Queries;

public record GetAuditLogsQuery(
    int PageNumber = 1,
    int PageSize = 25,
    string? SearchTerm = null,
    string? ActorEmail = null,
    Guid? TargetTenantId = null,
    string? Action = null,
    AuditCategory? Category = null,
    AuditSeverity? Severity = null,
    DateTime? DateFromUtc = null,
    DateTime? DateToUtc = null,
    bool IncludeArchived = false) : IRequest<Result<PagedAuditLogsDto>>;

public class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, Result<PagedAuditLogsDto>>
{
    private readonly DbContext _dbContext;

    public GetAuditLogsQueryHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PagedAuditLogsDto>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
        var pageSize = request.PageSize <= 0 ? 25 : (request.PageSize > 100 ? 100 : request.PageSize);

        var query = _dbContext.Set<AuditLog>().AsNoTracking().AsQueryable();

        if (!request.IncludeArchived)
        {
            query = query.Where(a => !a.IsArchived);
        }

        if (!string.IsNullOrWhiteSpace(request.ActorEmail))
        {
            var actor = request.ActorEmail.Trim();
            query = query.Where(a => a.AdminUserEmail.Contains(actor));
        }

        if (request.TargetTenantId.HasValue)
        {
            query = query.Where(a => a.TargetTenantId == request.TargetTenantId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            var action = request.Action.Trim();
            query = query.Where(a => a.Action.Contains(action));
        }

        if (request.Category.HasValue)
        {
            query = query.Where(a => a.Category == request.Category.Value);
        }

        if (request.Severity.HasValue)
        {
            query = query.Where(a => a.Severity == request.Severity.Value);
        }

        if (request.DateFromUtc.HasValue)
        {
            query = query.Where(a => a.TimestampUtc >= request.DateFromUtc.Value);
        }

        if (request.DateToUtc.HasValue)
        {
            query = query.Where(a => a.TimestampUtc <= request.DateToUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(a =>
                a.Action.Contains(term) ||
                a.Resource.Contains(term) ||
                a.AdminUserEmail.Contains(term) ||
                (a.TargetTenantName != null && a.TargetTenantName.Contains(term)) ||
                (a.DetailsJson != null && a.DetailsJson.Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var logs = await query
            .OrderByDescending(a => a.TimestampUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = logs.Select(a => new AuditLogSummaryDto(
            a.Id,
            a.TimestampUtc,
            a.AdminUserId,
            a.AdminUserEmail,
            a.ActorRole,
            a.Action,
            a.Category,
            a.Severity,
            a.Resource,
            a.TargetTenantId,
            a.TargetTenantName,
            a.IpAddress,
            a.RecordHash,
            a.VerifyIntegrity(),
            a.IsArchived)).ToList();

        return Result.Success(new PagedAuditLogsDto(items, totalCount, pageNumber, pageSize, totalPages));
    }
}

public record GetAuditLogByIdQuery(Guid Id) : IRequest<Result<AuditLogDetailDto>>;

public class GetAuditLogByIdQueryHandler : IRequestHandler<GetAuditLogByIdQuery, Result<AuditLogDetailDto>>
{
    private readonly DbContext _dbContext;

    public GetAuditLogByIdQueryHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<AuditLogDetailDto>> Handle(GetAuditLogByIdQuery request, CancellationToken cancellationToken)
    {
        var log = await _dbContext.Set<AuditLog>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);

        if (log is null)
        {
            return Result.Failure<AuditLogDetailDto>(Error.NotFound("Audit.NotFound", $"Registro de auditoria com ID '{request.Id}' não foi encontrado."));
        }

        var dto = new AuditLogDetailDto(
            log.Id,
            log.TimestampUtc,
            log.AdminUserId,
            log.AdminUserEmail,
            log.ActorRole,
            log.Action,
            log.Category,
            log.Severity,
            log.Resource,
            log.TargetTenantId,
            log.TargetTenantName,
            log.IpAddress,
            log.UserAgent,
            log.OldValuesJson,
            log.NewValuesJson,
            log.DetailsJson,
            log.RecordHash,
            log.PreviousRecordHash,
            log.VerifyIntegrity(),
            log.IsArchived);

        return Result.Success(dto);
    }
}

public record GetAuditStatsQuery : IRequest<Result<AuditStatsDto>>;

public class GetAuditStatsQueryHandler : IRequestHandler<GetAuditStatsQuery, Result<AuditStatsDto>>
{
    private readonly DbContext _dbContext;

    public GetAuditStatsQueryHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<AuditStatsDto>> Handle(GetAuditStatsQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var last24Hours = now.AddHours(-24);

        var query = _dbContext.Set<AuditLog>().AsNoTracking();

        var totalLogs = await query.CountAsync(cancellationToken);
        var logsLast24 = await query.CountAsync(a => a.TimestampUtc >= last24Hours, cancellationToken);
        var criticalCount = await query.CountAsync(a => a.Severity == AuditSeverity.Critical, cancellationToken);

        var categoryGrouping = await query
            .GroupBy(a => a.Category)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var severityGrouping = await query
            .GroupBy(a => a.Severity)
            .Select(g => new { Severity = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var countByCategory = categoryGrouping.ToDictionary(k => k.Category.ToString(), v => v.Count);
        var countBySeverity = severityGrouping.ToDictionary(k => k.Severity.ToString(), v => v.Count);

        // Check recent logs (up to 100) for integrity verification
        var recentLogs = await query
            .OrderByDescending(a => a.TimestampUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        var tamperedCount = recentLogs.Count(a => !a.VerifyIntegrity());
        var isChainValid = tamperedCount == 0;

        return Result.Success(new AuditStatsDto(
            totalLogs,
            logsLast24,
            criticalCount,
            tamperedCount,
            countByCategory,
            countBySeverity,
            isChainValid));
    }
}

public record VerifyAuditTrailIntegrityQuery(int MaxRecordsToCheck = 500) : IRequest<Result<AuditTrailVerificationResultDto>>;

public class VerifyAuditTrailIntegrityQueryHandler : IRequestHandler<VerifyAuditTrailIntegrityQuery, Result<AuditTrailVerificationResultDto>>
{
    private readonly DbContext _dbContext;

    public VerifyAuditTrailIntegrityQueryHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<AuditTrailVerificationResultDto>> Handle(VerifyAuditTrailIntegrityQuery request, CancellationToken cancellationToken)
    {
        var take = Math.Clamp(request.MaxRecordsToCheck, 10, 5000);

        var logs = await _dbContext.Set<AuditLog>()
            .AsNoTracking()
            .OrderBy(a => a.TimestampUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

        int valid = 0;
        int tampered = 0;
        Guid? firstTamperedId = null;

        for (int i = 0; i < logs.Count; i++)
        {
            var current = logs[i];
            bool isHashValid = current.VerifyIntegrity();

            if (!isHashValid)
            {
                tampered++;
                firstTamperedId ??= current.Id;
            }
            else
            {
                valid++;
            }
        }

        bool isChainValid = tampered == 0;
        string message = isChainValid
            ? $"Verificação forense concluída com sucesso: {valid} registros analisados e 100% íntegros."
            : $"Alerta forense: Detectada quebra de integridade em {tampered} registro(s). Primeiro ID afetado: {firstTamperedId}.";

        return Result.Success(new AuditTrailVerificationResultDto(
            isChainValid,
            logs.Count,
            valid,
            tampered,
            firstTamperedId,
            message,
            DateTime.UtcNow));
    }
}

public record ExportAuditTrailQuery(
    string? SearchTerm = null,
    string? ActorEmail = null,
    Guid? TargetTenantId = null,
    AuditCategory? Category = null,
    AuditSeverity? Severity = null,
    DateTime? DateFromUtc = null,
    DateTime? DateToUtc = null,
    string Format = "csv") : IRequest<Result<AuditExportFileDto>>;

public class ExportAuditTrailQueryHandler : IRequestHandler<ExportAuditTrailQuery, Result<AuditExportFileDto>>
{
    private readonly DbContext _dbContext;

    public ExportAuditTrailQueryHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<AuditExportFileDto>> Handle(ExportAuditTrailQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Set<AuditLog>().AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.ActorEmail))
        {
            var actor = request.ActorEmail.Trim();
            query = query.Where(a => a.AdminUserEmail.Contains(actor));
        }

        if (request.TargetTenantId.HasValue)
        {
            query = query.Where(a => a.TargetTenantId == request.TargetTenantId.Value);
        }

        if (request.Category.HasValue)
        {
            query = query.Where(a => a.Category == request.Category.Value);
        }

        if (request.Severity.HasValue)
        {
            query = query.Where(a => a.Severity == request.Severity.Value);
        }

        if (request.DateFromUtc.HasValue)
        {
            query = query.Where(a => a.TimestampUtc >= request.DateFromUtc.Value);
        }

        if (request.DateToUtc.HasValue)
        {
            query = query.Where(a => a.TimestampUtc <= request.DateToUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(a =>
                a.Action.Contains(term) ||
                a.Resource.Contains(term) ||
                a.AdminUserEmail.Contains(term));
        }

        var logs = await query
            .OrderByDescending(a => a.TimestampUtc)
            .Take(5000)
            .ToListAsync(cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("Id,TimestampUtc,ActorEmail,ActorRole,Action,Category,Severity,Resource,TargetTenantId,TargetTenantName,IpAddress,RecordHash,IsIntegrityValid");

        foreach (var l in logs)
        {
            var valid = l.VerifyIntegrity();
            sb.AppendLine($"\"{l.Id}\",\"{l.TimestampUtc:O}\",\"{Escape(l.AdminUserEmail)}\",\"{Escape(l.ActorRole)}\",\"{Escape(l.Action)}\",\"{l.Category}\",\"{l.Severity}\",\"{Escape(l.Resource)}\",\"{l.TargetTenantId}\",\"{Escape(l.TargetTenantName)}\",\"{l.IpAddress}\",\"{l.RecordHash}\",\"{valid}\"");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"audit-trail-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";

        return Result.Success(new AuditExportFileDto(fileName, "text/csv; charset=utf-8", bytes));
    }

    private static string Escape(string? value) => (value ?? string.Empty).Replace("\"", "\"\"");
}

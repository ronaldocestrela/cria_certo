using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Features.Compliance.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Compliance.Queries;

public record ExportAccessTrailQuery(
    Guid ActorId,
    string ActorEmail,
    string? ActorRole,
    string IpAddress,
    string? UserAgent,
    ExportAccessTrailRequest Request
) : IRequest<Result<ComplianceDossierExportDto>>;

public class ExportAccessTrailQueryHandler : IRequestHandler<ExportAccessTrailQuery, Result<ComplianceDossierExportDto>>
{
    private readonly DbContext _dbContext;

    public ExportAccessTrailQueryHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<ComplianceDossierExportDto>> Handle(ExportAccessTrailQuery command, CancellationToken cancellationToken)
    {
        var req = command.Request;
        if (string.IsNullOrWhiteSpace(req.Purpose))
        {
            return Result.Failure<ComplianceDossierExportDto>(ComplianceErrors.ExportPurposeRequired);
        }

        var query = _dbContext.Set<AuditLog>().AsNoTracking().AsQueryable();

        // Filtrar por eventos relacionados a dados pessoais e compliance
        query = query.Where(a =>
            a.Category == AuditCategory.Compliance ||
            a.Action.Contains("UNMASK") ||
            a.Action.Contains("IMPERSONATION") ||
            a.Action.Contains("ACCESS") ||
            a.Action.Contains("REMEDIATION") ||
            a.Category == AuditCategory.Security);

        if (req.TargetTenantId.HasValue)
        {
            query = query.Where(a => a.TargetTenantId == req.TargetTenantId.Value);
        }

        if (!string.IsNullOrWhiteSpace(req.ActorEmail))
        {
            var emailTerm = req.ActorEmail.Trim().ToLower();
            query = query.Where(a => a.AdminUserEmail.ToLower().Contains(emailTerm));
        }

        if (req.DateFromUtc.HasValue)
        {
            query = query.Where(a => a.TimestampUtc >= req.DateFromUtc.Value);
        }

        if (req.DateToUtc.HasValue)
        {
            query = query.Where(a => a.TimestampUtc <= req.DateToUtc.Value);
        }

        var records = await query
            .OrderByDescending(a => a.TimestampUtc)
            .Take(5000)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        byte[] contentBytes;
        string fileName;
        string contentType;

        if (req.Format?.Equals("JSON", StringComparison.OrdinalIgnoreCase) == true)
        {
            fileName = $"dossie-acesso-lgpd-{now:yyyyMMdd-HHmmss}.json";
            contentType = "application/json";

            var exportObject = new
            {
                Title = "DOSSIÊ FORENSE DE ACESSO A DADOS PESSOAIS - LGPD",
                Purpose = req.Purpose.Trim(),
                GeneratedAtUtc = now,
                GeneratedBy = command.ActorEmail,
                TotalRecords = records.Count,
                Records = records.Select(r => new
                {
                    r.Id,
                    r.TimestampUtc,
                    r.AdminUserEmail,
                    r.ActorRole,
                    r.Action,
                    Category = r.Category.ToString(),
                    Severity = r.Severity.ToString(),
                    r.Resource,
                    r.TargetTenantName,
                    r.IpAddress,
                    r.RecordHash,
                    IsIntegrityValid = r.VerifyIntegrity()
                })
            };

            var jsonString = JsonSerializer.Serialize(exportObject, new JsonSerializerOptions { WriteIndented = true });
            contentBytes = Encoding.UTF8.GetBytes(jsonString);
        }
        else
        {
            fileName = $"dossie-acesso-lgpd-{now:yyyyMMdd-HHmmss}.csv";
            contentType = "text/csv; charset=utf-8";

            var sb = new StringBuilder();
            sb.AppendLine("# DOSSIÊ FORENSE DE CONFORMIDADE LGPD - CRIACERTO SAAS");
            sb.AppendLine($"# Finalidade Declarada: \"{EscapeCsv(req.Purpose.Trim())}\"");
            sb.AppendLine($"# Solicitante / Auditor: {command.ActorEmail}");
            sb.AppendLine($"# Emitido em UTC: {now:O}");
            sb.AppendLine($"# Total de Registros: {records.Count}");
            sb.AppendLine("ID,DataHoraUtc,OperadorEmail,OperadorPapel,Acao,Categoria,Severidade,Recurso,TenantAlvo,EnderecoIp,HashRegistro,IntegridadeValida");

            foreach (var r in records)
            {
                sb.AppendLine(string.Join(",",
                    r.Id,
                    r.TimestampUtc.ToString("O"),
                    EscapeCsv(r.AdminUserEmail),
                    EscapeCsv(r.ActorRole ?? string.Empty),
                    EscapeCsv(r.Action),
                    r.Category,
                    r.Severity,
                    EscapeCsv(r.Resource),
                    EscapeCsv(r.TargetTenantName ?? string.Empty),
                    EscapeCsv(r.IpAddress),
                    r.RecordHash,
                    r.VerifyIntegrity()
                ));
            }

            contentBytes = Encoding.UTF8.GetBytes(sb.ToString());
        }

        var sha256Hex = Convert.ToHexString(SHA256.HashData(contentBytes)).ToLowerInvariant();

        // Obter hash do último log de auditoria para encadeamento
        var lastLog = await _dbContext.Set<AuditLog>()
            .OrderByDescending(a => a.TimestampUtc)
            .FirstOrDefaultAsync(cancellationToken);

        string? previousHash = lastLog?.RecordHash;

        var auditDetails = JsonSerializer.Serialize(new
        {
            Purpose = req.Purpose.Trim(),
            Format = req.Format ?? "CSV",
            TargetTenantId = req.TargetTenantId,
            ActorEmailFilter = req.ActorEmail,
            TotalRecordsExported = records.Count,
            DossierSha256 = sha256Hex
        });

        // Registrar exportação compulsória na trilha forense com severidade crítica
        var auditLog = AuditLog.CreateForensic(
            adminUserId: command.ActorId,
            adminUserEmail: command.ActorEmail,
            actorRole: command.ActorRole,
            action: "COMPLIANCE_DOSSIER_EXPORTED",
            category: AuditCategory.Compliance,
            severity: AuditSeverity.Critical,
            resource: "Compliance:AccessTrailDossier",
            targetTenantId: req.TargetTenantId,
            targetTenantName: null,
            ipAddress: command.IpAddress,
            userAgent: command.UserAgent,
            oldValuesJson: null,
            newValuesJson: null,
            previousRecordHash: previousHash,
            detailsJson: auditDetails
        );

        _dbContext.Set<AuditLog>().Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var exportDto = new ComplianceDossierExportDto(
            FileName: fileName,
            ContentType: contentType,
            Content: contentBytes,
            Sha256Hash: sha256Hex,
            GeneratedAtUtc: now
        );

        return Result.Success(exportDto);
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return $"\"{value}\"";
    }
}

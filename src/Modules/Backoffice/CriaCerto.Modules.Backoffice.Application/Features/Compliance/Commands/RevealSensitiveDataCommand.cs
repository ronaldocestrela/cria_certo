using System.Text.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Domain.Services;
using CriaCerto.Modules.Backoffice.Application.Features.Compliance.Dtos;
using CriaCerto.Modules.Tenancy.Application.Contracts;
using CriaCerto.Modules.Tenancy.Application.Features.BackofficeTenants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Compliance.Commands;

public record RevealSensitiveDataCommand(
    Guid ActorId,
    string ActorEmail,
    string? ActorRole,
    string IpAddress,
    string? UserAgent,
    RevealSensitiveDataRequest Request
) : IRequest<Result<RevealedDataResultDto>>;

public class RevealSensitiveDataCommandHandler : IRequestHandler<RevealSensitiveDataCommand, Result<RevealedDataResultDto>>
{
    private readonly DbContext _dbContext;
    private readonly ISender _sender;
    private readonly IPiiDataMasker _masker;

    public RevealSensitiveDataCommandHandler(
        DbContext dbContext,
        ISender sender,
        IPiiDataMasker masker)
    {
        _dbContext = dbContext;
        _sender = sender;
        _masker = masker;
    }

    public async Task<Result<RevealedDataResultDto>> Handle(RevealSensitiveDataCommand command, CancellationToken cancellationToken)
    {
        var req = command.Request;

        if (string.IsNullOrWhiteSpace(req.Justification) || req.Justification.Trim().Length < 10)
        {
            return Result.Failure<RevealedDataResultDto>(ComplianceErrors.JustificationRequired);
        }

        string plainValue;
        string maskedValue;
        string? targetTenantName = null;
        Guid? targetTenantId = null;

        var entityType = req.EntityType?.Trim() ?? string.Empty;
        var fieldName = req.FieldName?.Trim() ?? string.Empty;

        if (entityType.Equals("Tenant", StringComparison.OrdinalIgnoreCase))
        {
            var tenantResult = await _sender.Send(new GetTenantBackofficeDetailQuery(req.EntityId), cancellationToken);
            if (tenantResult.IsFailure)
            {
                return Result.Failure<RevealedDataResultDto>(ComplianceErrors.TargetEntityNotFound);
            }

            var t = tenantResult.Value;
            targetTenantId = t.Id;
            targetTenantName = t.Name;

            switch (fieldName.ToLowerInvariant())
            {
                case "cnpj":
                case "document":
                    plainValue = t.CNPJ;
                    maskedValue = _masker.MaskDocument(plainValue);
                    break;
                case "technicalowneremail":
                    plainValue = t.TechnicalOwnerEmail ?? string.Empty;
                    maskedValue = _masker.MaskEmail(plainValue);
                    break;
                case "commercialowneremail":
                    plainValue = t.CommercialOwnerEmail ?? string.Empty;
                    maskedValue = _masker.MaskEmail(plainValue);
                    break;
                case "technicalownername":
                    plainValue = t.TechnicalOwnerName ?? string.Empty;
                    maskedValue = _masker.MaskPersonName(plainValue);
                    break;
                case "commercialownername":
                    plainValue = t.CommercialOwnerName ?? string.Empty;
                    maskedValue = _masker.MaskPersonName(plainValue);
                    break;
                default:
                    return Result.Failure<RevealedDataResultDto>(ComplianceErrors.UnsupportedPiiField);
            }
        }
        else if (entityType.Equals("AdminUser", StringComparison.OrdinalIgnoreCase))
        {
            var user = await _dbContext.Set<AdminUser>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == req.EntityId, cancellationToken);

            if (user is null)
            {
                return Result.Failure<RevealedDataResultDto>(ComplianceErrors.TargetEntityNotFound);
            }

            switch (fieldName.ToLowerInvariant())
            {
                case "email":
                    plainValue = user.Email;
                    maskedValue = _masker.MaskEmail(plainValue);
                    break;
                case "name":
                    plainValue = user.Name;
                    maskedValue = _masker.MaskPersonName(plainValue);
                    break;
                default:
                    return Result.Failure<RevealedDataResultDto>(ComplianceErrors.UnsupportedPiiField);
            }
        }
        else
        {
            return Result.Failure<RevealedDataResultDto>(ComplianceErrors.UnsupportedPiiField);
        }

        // Obter hash do último log de auditoria para encadeamento criptográfico SHA-256
        var lastLog = await _dbContext.Set<AuditLog>()
            .OrderByDescending(a => a.TimestampUtc)
            .FirstOrDefaultAsync(cancellationToken);

        string? previousHash = lastLog?.RecordHash;

        var auditDetails = JsonSerializer.Serialize(new
        {
            Justification = req.Justification.Trim(),
            EntityType = entityType,
            EntityId = req.EntityId,
            FieldName = fieldName,
            MaskedSnapshot = maskedValue
        });

        var auditLog = AuditLog.CreateForensic(
            adminUserId: command.ActorId,
            adminUserEmail: command.ActorEmail,
            actorRole: command.ActorRole,
            action: "PII_DATA_UNMASKED",
            category: AuditCategory.Compliance,
            severity: AuditSeverity.High,
            resource: $"{entityType}:{req.EntityId}/{fieldName}",
            targetTenantId: targetTenantId,
            targetTenantName: targetTenantName,
            ipAddress: command.IpAddress,
            userAgent: command.UserAgent,
            oldValuesJson: null,
            newValuesJson: null,
            previousRecordHash: previousHash,
            detailsJson: auditDetails
        );

        _dbContext.Set<AuditLog>().Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var resultDto = new RevealedDataResultDto(
            FieldName: fieldName,
            PlainValue: plainValue,
            MaskedValue: maskedValue,
            AuditLogId: auditLog.Id,
            RevealedAtUtc: DateTime.UtcNow
        );

        return Result.Success(resultDto);
    }
}

using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Features.ExportUserDataPortability.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CriaCerto.Api.Services;

public sealed class TenancyBackofficeAuditSink : ITenancyAuditSink, IUserAuditTrailLookupService
{
    private readonly BackofficeDbContext _dbContext;
    private readonly ILogger<TenancyBackofficeAuditSink> _logger;

    public TenancyBackofficeAuditSink(
        BackofficeDbContext dbContext,
        ILogger<TenancyBackofficeAuditSink> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task RecordUserConsentAuditAsync(
        Guid userId,
        string userEmail,
        string termsVersion,
        string privacyPolicyVersion,
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var previousHash = await _dbContext.AuditLogs
                .AsNoTracking()
                .OrderByDescending(a => a.TimestampUtc)
                .Select(a => a.RecordHash)
                .FirstOrDefaultAsync(cancellationToken);

            var detailsJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                UserId = userId,
                UserEmail = userEmail,
                TermsVersion = termsVersion,
                PrivacyPolicyVersion = privacyPolicyVersion,
                IpAddress = ipAddress,
                Action = "USER_CONSENT_REGISTERED",
                TimestampUtc = DateTime.UtcNow
            });

            var audit = AuditLog.CreateForensic(
                adminUserId: userId,
                adminUserEmail: userEmail,
                actorRole: "RegisteredUser",
                action: "USER_CONSENT_REGISTERED",
                category: AuditCategory.Compliance,
                severity: AuditSeverity.High,
                resource: $"Users/{userId}",
                targetTenantId: null,
                targetTenantName: null,
                ipAddress: string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress,
                userAgent: "WebRegistrationFlow/1.0",
                previousRecordHash: previousHash,
                detailsJson: detailsJson);

            _dbContext.AuditLogs.Add(audit);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Auditoria forense de consentimento LGPD registrada para usuário {UserId} ({Email}) com hash {Hash}.",
                userId, userEmail, audit.RecordHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falha ao registrar auditoria forense de consentimento LGPD para usuário {UserId} ({Email}).",
                userId, userEmail);
        }
    }

    public async Task RecordUserProfileUpdateAuditAsync(
        Guid userId,
        string userEmail,
        string oldFullName,
        string newFullName,
        string? oldPhone,
        string? newPhone,
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var previousHash = await _dbContext.AuditLogs
                .AsNoTracking()
                .OrderByDescending(a => a.TimestampUtc)
                .Select(a => a.RecordHash)
                .FirstOrDefaultAsync(cancellationToken);

            var oldValues = System.Text.Json.JsonSerializer.Serialize(new
            {
                FullName = oldFullName,
                PhoneNumber = oldPhone
            });

            var newValues = System.Text.Json.JsonSerializer.Serialize(new
            {
                FullName = newFullName,
                PhoneNumber = newPhone
            });

            var audit = AuditLog.CreateForensic(
                adminUserId: userId,
                adminUserEmail: userEmail,
                actorRole: "RegisteredUser",
                action: "USER_PROFILE_RECTIFIED",
                category: AuditCategory.Compliance,
                severity: AuditSeverity.Medium,
                resource: $"Users/{userId}",
                targetTenantId: null,
                targetTenantName: null,
                ipAddress: string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress,
                userAgent: "WebUserProfileFlow/1.0",
                previousRecordHash: previousHash,
                oldValuesJson: oldValues,
                newValuesJson: newValues,
                detailsJson: "{\"Reason\":\"DataSubjectProfileRectification_Art18_III\"}");

            _dbContext.AuditLogs.Add(audit);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Auditoria forense de retificação de perfil LGPD registrada para usuário {UserId} ({Email}) com hash {Hash}.",
                userId, userEmail, audit.RecordHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falha ao registrar auditoria forense de retificação de perfil para usuário {UserId} ({Email}).",
                userId, userEmail);
        }
    }

    public async Task RecordUserPasswordChangeAuditAsync(
        Guid userId,
        string userEmail,
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var previousHash = await _dbContext.AuditLogs
                .AsNoTracking()
                .OrderByDescending(a => a.TimestampUtc)
                .Select(a => a.RecordHash)
                .FirstOrDefaultAsync(cancellationToken);

            var audit = AuditLog.CreateForensic(
                adminUserId: userId,
                adminUserEmail: userEmail,
                actorRole: "RegisteredUser",
                action: "USER_PASSWORD_CHANGED",
                category: AuditCategory.Security,
                severity: AuditSeverity.High,
                resource: $"Users/{userId}",
                targetTenantId: null,
                targetTenantName: null,
                ipAddress: string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress,
                userAgent: "WebUserProfileFlow/1.0",
                previousRecordHash: previousHash,
                detailsJson: "{\"Action\":\"PasswordCredentialUpdated\"}");

            _dbContext.AuditLogs.Add(audit);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Auditoria forense de alteração de senha registrada para usuário {UserId} ({Email}) com hash {Hash}.",
                userId, userEmail, audit.RecordHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falha ao registrar auditoria forense de alteração de senha para usuário {UserId} ({Email}).",
                userId, userEmail);
        }
    }

    public async Task RecordDataSubjectPortabilityExportAuditAsync(
        Guid userId,
        string userEmail,
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var previousHash = await _dbContext.AuditLogs
                .AsNoTracking()
                .OrderByDescending(a => a.TimestampUtc)
                .Select(a => a.RecordHash)
                .FirstOrDefaultAsync(cancellationToken);

            var audit = AuditLog.CreateForensic(
                adminUserId: userId,
                adminUserEmail: userEmail,
                actorRole: "RegisteredUser",
                action: "DATA_SUBJECT_PORTABILITY_EXPORTED",
                category: AuditCategory.Compliance,
                severity: AuditSeverity.High,
                resource: $"Users/{userId}",
                targetTenantId: null,
                targetTenantName: null,
                ipAddress: string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress,
                userAgent: "WebDataPortabilityFlow/1.0",
                previousRecordHash: previousHash,
                detailsJson: "{\"Action\":\"DataSubjectPortabilityExported_Art18_V\"}");

            _dbContext.AuditLogs.Add(audit);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Auditoria forense de portabilidade de dados LGPD registrada para usuário {UserId} ({Email}) com hash {Hash}.",
                userId, userEmail, audit.RecordHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falha ao registrar auditoria forense de portabilidade de dados para usuário {UserId} ({Email}).",
                userId, userEmail);
        }
    }

    public async Task<IReadOnlyList<UserAuditTrailEntryExportDto>> GetUserAuditTrailAsync(
        Guid userId,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userResource = $"Users/{userId}";
            var logs = await _dbContext.AuditLogs
                .AsNoTracking()
                .Where(a => a.AdminUserId == userId || a.Resource == userResource)
                .OrderByDescending(a => a.TimestampUtc)
                .Take(limit)
                .ToListAsync(cancellationToken);

            return logs.Select(a => new UserAuditTrailEntryExportDto(
                a.TimestampUtc,
                a.Action,
                a.Category.ToString(),
                a.Severity.ToString(),
                a.IpAddress,
                a.DetailsJson ?? string.Empty
            )).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar trilha forense do usuário {UserId} para portabilidade LGPD.", userId);
            return Array.Empty<UserAuditTrailEntryExportDto>();
        }
    }

    public async Task RecordUserAccountAnonymizedAuditAsync(
        Guid userId,
        string originalEmail,
        string anonymizedEmail,
        string? exitReason,
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var previousHash = await _dbContext.AuditLogs
                .AsNoTracking()
                .OrderByDescending(a => a.TimestampUtc)
                .Select(a => a.RecordHash)
                .FirstOrDefaultAsync(cancellationToken);

            var detailsJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                UserId = userId,
                OriginalEmail = originalEmail,
                AnonymizedEmail = anonymizedEmail,
                ExitReason = exitReason,
                Action = "USER_ACCOUNT_ANONYMIZED",
                Specification = "LGPD Art. 18, VI",
                TimestampUtc = DateTime.UtcNow
            }, new System.Text.Json.JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            var audit = AuditLog.CreateForensic(
                adminUserId: userId,
                adminUserEmail: anonymizedEmail,
                actorRole: "RegisteredUser",
                action: "USER_ACCOUNT_ANONYMIZED",
                category: AuditCategory.Compliance,
                severity: AuditSeverity.Critical,
                resource: $"Users/{userId}",
                targetTenantId: null,
                targetTenantName: null,
                ipAddress: string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress,
                userAgent: "WebAccountAnonymizationFlow/1.0",
                previousRecordHash: previousHash,
                detailsJson: detailsJson);

            _dbContext.AuditLogs.Add(audit);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Auditoria forense de encerramento e anonimização de conta LGPD registrada para usuário {UserId} ({OriginalEmail} -> {AnonymizedEmail}) com hash {Hash}.",
                userId, originalEmail, anonymizedEmail, audit.RecordHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falha ao registrar auditoria forense de anonimização de conta para usuário {UserId} ({OriginalEmail}).",
                userId, originalEmail);
        }
    }
}


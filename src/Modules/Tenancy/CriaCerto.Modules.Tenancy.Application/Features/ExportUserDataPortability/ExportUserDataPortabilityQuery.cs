using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Features.ExportUserDataPortability.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.ExportUserDataPortability;

public sealed record ExportUserDataPortabilityQuery(
    Guid UserId,
    string? ClientIpAddress = null
) : IRequest<Result<PortabilityFileExportDto>>;

public sealed class ExportUserDataPortabilityQueryHandler : IRequestHandler<ExportUserDataPortabilityQuery, Result<PortabilityFileExportDto>>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly ITenancyDbContext _dbContext;
    private readonly ITenancyAuditSink? _auditSink;
    private readonly IUserAuditTrailLookupService? _auditTrailLookupService;

    public ExportUserDataPortabilityQueryHandler(
        ITenancyDbContext dbContext,
        ITenancyAuditSink? auditSink = null,
        IUserAuditTrailLookupService? auditTrailLookupService = null)
    {
        _dbContext = dbContext;
        _auditSink = auditSink;
        _auditTrailLookupService = auditTrailLookupService;
    }

    public async Task<Result<PortabilityFileExportDto>> Handle(ExportUserDataPortabilityQuery request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.UserTenants)
            .ThenInclude(ut => ut.Tenant)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null)
        {
            return Result.Failure<PortabilityFileExportDto>(
                Error.NotFound("User.NotFound", "Usuário não encontrado."));
        }

        var associatedTenants = user.UserTenants
            .Where(ut => ut.Tenant != null)
            .Select(ut => new TenantAffiliationExportDto(
                ut.TenantId,
                ut.Tenant!.Name,
                ut.Role.ToString(),
                ut.JoinedAt,
                ut.Tenant.SubscribedPlan))
            .ToList();

        var auditTrail = _auditTrailLookupService != null
            ? await _auditTrailLookupService.GetUserAuditTrailAsync(user.Id, 100, cancellationToken)
            : Array.Empty<UserAuditTrailEntryExportDto>();

        var userProfile = new UserDataExportDto(
            user.Id,
            user.FullName,
            user.Email,
            user.PhoneNumber,
            user.IsEmailConfirmed,
            user.EmailConfirmedAtUtc,
            user.CreatedAtUtc
        );

        var consentHistory = new ConsentMetadataExportDto(
            user.TermsVersion,
            user.PrivacyPolicyVersion,
            user.TermsAcceptedAtUtc,
            user.PrivacyPolicyAcceptedAtUtc,
            user.ConsentIpAddress
        );

        var dossier = new DataSubjectPortabilityDossierDto(
            Specification: "LGPD (Lei nº 13.709/2018) - Art. 18, II e V: Acesso e Portabilidade dos Dados Pessoais",
            Purpose: "Exercício do direito à portabilidade de dados pessoais em formato canônico interoperável estruturado",
            ControllerName: "CriaCerto Agropecuária Digital Ltda.",
            ExportedAtUtc: DateTime.UtcNow,
            UserProfile: userProfile,
            ConsentHistory: consentHistory,
            AssociatedTenants: associatedTenants,
            AuditTrail: auditTrail
        );

        var jsonString = JsonSerializer.Serialize(dossier, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(jsonString);

        if (_auditSink != null)
        {
            await _auditSink.RecordDataSubjectPortabilityExportAuditAsync(
                user.Id,
                user.Email,
                request.ClientIpAddress ?? "127.0.0.1",
                cancellationToken);
        }

        var fileName = $"meus-dados-lgpd-{user.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}.json";

        return Result.Success(new PortabilityFileExportDto(
            fileName,
            "application/json",
            bytes
        ));
    }
}

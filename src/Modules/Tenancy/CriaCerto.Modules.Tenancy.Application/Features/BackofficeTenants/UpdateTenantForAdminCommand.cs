using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Contracts;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Domain.Errors;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.BackofficeTenants;

public record UpdateTenantForAdminCommand(
    Guid TenantId,
    string Name,
    string? LegalName,
    string CNPJ,
    string? ExternalIdentifier,
    string State,
    string City,
    string StateRegistration,
    decimal AreaInHectares,
    int Capacity,
    string Type,
    string? TechnicalOwnerName,
    string? TechnicalOwnerEmail,
    string? CommercialOwnerName,
    string? CommercialOwnerEmail
) : IRequest<Result<TenantBackofficeDetailDto>>;

public sealed class UpdateTenantForAdminCommandValidator : AbstractValidator<UpdateTenantForAdminCommand>
{
    public UpdateTenantForAdminCommandValidator(ITenancyDbContext dbContext)
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("O ID do tenant é obrigatório.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome da fazenda é obrigatório.")
            .MinimumLength(3).WithMessage("O nome da fazenda deve ter no mínimo 3 caracteres.")
            .MaximumLength(150).WithMessage("O nome da fazenda deve ter no máximo 150 caracteres.");

        RuleFor(x => x.CNPJ)
            .NotEmpty().WithMessage("O CNPJ/CPF é obrigatório.")
            .Must(CnpjNormalizer.IsValidCnpjOrCpf).WithMessage("O CNPJ ou CPF informado é inválido.");

        RuleFor(x => x.State)
            .NotEmpty().WithMessage("O estado (UF) é obrigatório.")
            .Length(2).WithMessage("O estado (UF) deve ter exatamente 2 letras.");

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("O município é obrigatório.");

        RuleFor(x => x.AreaInHectares)
            .GreaterThanOrEqualTo(0).WithMessage("A área em hectares não pode ser negativa.");

        RuleFor(x => x.Capacity)
            .GreaterThan(0).WithMessage("A capacidade de cabeças deve ser maior que zero.");

        RuleFor(x => x)
            .MustAsync(async (cmd, cancellation) =>
            {
                var tenant = await dbContext.Tenants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == cmd.TenantId, cancellation);

                if (tenant is null) return true;

                return PlanCapacityLimits.IsCapacityWithinPlan(tenant.SubscribedPlan, cmd.Capacity);
            })
            .WithMessage("A capacidade solicitada excede o limite permitido para o plano contratado.");
    }
}

public sealed class UpdateTenantForAdminCommandHandler : IRequestHandler<UpdateTenantForAdminCommand, Result<TenantBackofficeDetailDto>>
{
    private readonly ITenancyDbContext _dbContext;

    public UpdateTenantForAdminCommandHandler(ITenancyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<TenantBackofficeDetailDto>> Handle(UpdateTenantForAdminCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<TenantBackofficeDetailDto>(TenancyErrors.TenantNotFound);
        }

        var cnpjNormalized = CnpjNormalizer.Normalize(request.CNPJ);
        if (!CnpjNormalizer.IsValidCnpjOrCpf(request.CNPJ))
        {
            return Result.Failure<TenantBackofficeDetailDto>(TenancyErrors.InvalidCnpj);
        }

        var cnpjConflict = await _dbContext.Tenants
            .AnyAsync(t => t.CnpjNormalized == cnpjNormalized && t.Id != request.TenantId, cancellationToken);
        if (cnpjConflict)
        {
            return Result.Failure<TenantBackofficeDetailDto>(TenancyErrors.CnpjAlreadyExists);
        }

        var externalId = string.IsNullOrWhiteSpace(request.ExternalIdentifier) ? null : request.ExternalIdentifier.Trim();
        if (externalId is not null)
        {
            var externalConflict = await _dbContext.Tenants
                .AnyAsync(t => t.ExternalIdentifier != null
                    && t.ExternalIdentifier.ToLower() == externalId.ToLower()
                    && t.Id != request.TenantId, cancellationToken);
            if (externalConflict)
            {
                return Result.Failure<TenantBackofficeDetailDto>(TenancyErrors.ExternalIdentifierAlreadyExists);
            }
        }

        if (!PlanCapacityLimits.IsCapacityWithinPlan(tenant.SubscribedPlan, request.Capacity))
        {
            return Result.Failure<TenantBackofficeDetailDto>(TenancyErrors.CapacityExceedsPlan);
        }

        tenant.Name = request.Name.Trim();
        tenant.LegalName = string.IsNullOrWhiteSpace(request.LegalName) ? null : request.LegalName.Trim();
        tenant.CNPJ = request.CNPJ.Trim();
        tenant.CnpjNormalized = cnpjNormalized;
        tenant.ExternalIdentifier = externalId;
        tenant.State = request.State.Trim().ToUpperInvariant();
        tenant.City = request.City.Trim();
        tenant.StateRegistration = request.StateRegistration?.Trim() ?? string.Empty;
        tenant.AreaInHectares = request.AreaInHectares;
        tenant.Capacity = request.Capacity;
        tenant.Type = request.Type.Trim();
        tenant.TechnicalOwnerName = TrimOrNull(request.TechnicalOwnerName);
        tenant.TechnicalOwnerEmail = TrimOrNull(request.TechnicalOwnerEmail);
        tenant.CommercialOwnerName = TrimOrNull(request.CommercialOwnerName);
        tenant.CommercialOwnerEmail = TrimOrNull(request.CommercialOwnerEmail);
        tenant.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var teamCount = await _dbContext.UserTenants.CountAsync(ut => ut.TenantId == tenant.Id, cancellationToken);
        var unitCount = await _dbContext.ProductionUnits.CountAsync(pu => pu.TenantId == tenant.Id, cancellationToken);

        return Result.Success(TenantBackofficeMapper.ToDetailDto(tenant, teamCount, unitCount));
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

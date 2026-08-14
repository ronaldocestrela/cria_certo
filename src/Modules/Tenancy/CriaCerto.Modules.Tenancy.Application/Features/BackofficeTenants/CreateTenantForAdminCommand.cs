using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.BuildingBlocks.Abstractions.Tenancy;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Contracts;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Domain.Errors;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.BackofficeTenants;

public record CreateTenantForAdminCommand(
    string Name,
    string? LegalName,
    string CNPJ,
    string? ExternalIdentifier,
    string State,
    string City,
    string StateRegistration,
    decimal AreaInHectares,
    string SubscribedPlan,
    int Capacity,
    string Type,
    string? TechnicalOwnerName,
    string? TechnicalOwnerEmail,
    string? CommercialOwnerName,
    string? CommercialOwnerEmail,
    string? OwnerUserEmail
) : IRequest<Result<TenantBackofficeDetailDto>>;

public sealed class CreateTenantForAdminCommandValidator : AbstractValidator<CreateTenantForAdminCommand>
{
    private static readonly string[] AllowedPlans = ["Starter", "Pro", "Enterprise"];

    public CreateTenantForAdminCommandValidator()
    {
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

        RuleFor(x => x.SubscribedPlan)
            .Must(plan => AllowedPlans.Contains(plan, StringComparer.OrdinalIgnoreCase))
            .WithMessage("O plano selecionado é inválido. Escolha entre Starter, Pro ou Enterprise.");

        RuleFor(x => x)
            .Must(cmd => PlanCapacityLimits.IsCapacityWithinPlan(cmd.SubscribedPlan, cmd.Capacity))
            .WithMessage("A capacidade solicitada excede o limite permitido para o plano contratado.");
    }
}

public sealed class CreateTenantForAdminCommandHandler : IRequestHandler<CreateTenantForAdminCommand, Result<TenantBackofficeDetailDto>>
{
    private readonly ITenancyDbContext _dbContext;
    private readonly ITenantDatabaseProvisioner _provisioner;

    public CreateTenantForAdminCommandHandler(ITenancyDbContext dbContext, ITenantDatabaseProvisioner provisioner)
    {
        _dbContext = dbContext;
        _provisioner = provisioner;
    }

    public async Task<Result<TenantBackofficeDetailDto>> Handle(CreateTenantForAdminCommand request, CancellationToken cancellationToken)
    {
        var cnpjNormalized = CnpjNormalizer.Normalize(request.CNPJ);
        if (!CnpjNormalizer.IsValidCnpjOrCpf(request.CNPJ))
        {
            return Result.Failure<TenantBackofficeDetailDto>(TenancyErrors.InvalidCnpj);
        }

        var cnpjExists = await _dbContext.Tenants
            .AnyAsync(t => t.CnpjNormalized == cnpjNormalized, cancellationToken);
        if (cnpjExists)
        {
            return Result.Failure<TenantBackofficeDetailDto>(TenancyErrors.CnpjAlreadyExists);
        }

        var externalId = NormalizeExternalIdentifier(request.ExternalIdentifier);
        if (externalId is not null)
        {
            var externalExists = await _dbContext.Tenants
                .AnyAsync(t => t.ExternalIdentifier != null && t.ExternalIdentifier.ToLower() == externalId.ToLower(), cancellationToken);
            if (externalExists)
            {
                return Result.Failure<TenantBackofficeDetailDto>(TenancyErrors.ExternalIdentifierAlreadyExists);
            }
        }

        User? ownerUser = null;
        if (!string.IsNullOrWhiteSpace(request.OwnerUserEmail))
        {
            var normalizedEmail = request.OwnerUserEmail.Trim().ToLowerInvariant();
            ownerUser = await _dbContext.Users
                .Include(u => u.UserTenants)
                .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

            if (ownerUser is null)
            {
                return Result.Failure<TenantBackofficeDetailDto>(
                    Error.NotFound("User.NotFound", "Usuário produtor não encontrado para vinculação como owner."));
            }
        }

        var now = DateTime.UtcNow;
        var plan = request.SubscribedPlan.Trim();
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            LegalName = string.IsNullOrWhiteSpace(request.LegalName) ? null : request.LegalName.Trim(),
            CNPJ = request.CNPJ.Trim(),
            CnpjNormalized = cnpjNormalized,
            ExternalIdentifier = externalId,
            State = request.State.Trim().ToUpperInvariant(),
            City = request.City.Trim(),
            StateRegistration = request.StateRegistration?.Trim() ?? string.Empty,
            AreaInHectares = request.AreaInHectares,
            SubscribedPlan = plan,
            Capacity = request.Capacity,
            Status = "Active",
            Type = string.IsNullOrWhiteSpace(request.Type) ? "Pecuária de Corte e Cria" : request.Type.Trim(),
            TechnicalOwnerName = TrimOrNull(request.TechnicalOwnerName),
            TechnicalOwnerEmail = TrimOrNull(request.TechnicalOwnerEmail),
            CommercialOwnerName = TrimOrNull(request.CommercialOwnerName),
            CommercialOwnerEmail = TrimOrNull(request.CommercialOwnerEmail),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _dbContext.Tenants.Add(tenant);

        if (ownerUser is not null)
        {
            _dbContext.UserTenants.Add(new UserTenant
            {
                UserId = ownerUser.Id,
                User = ownerUser,
                TenantId = tenant.Id,
                Tenant = tenant,
                Role = UserRole.Admin
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _provisioner.EnsureTenantDatabaseAsync(tenant.Id, cancellationToken);

        var teamCount = ownerUser is not null ? 1 : 0;
        return Result.Success(TenantBackofficeMapper.ToDetailDto(tenant, teamCount, 0));
    }

    private static string? NormalizeExternalIdentifier(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

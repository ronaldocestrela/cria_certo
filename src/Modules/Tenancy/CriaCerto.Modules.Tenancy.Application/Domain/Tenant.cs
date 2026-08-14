using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Domain.Errors;

namespace CriaCerto.Modules.Tenancy.Application.Domain;

public sealed class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public string CNPJ { get; set; } = string.Empty;
    public string CnpjNormalized { get; set; } = string.Empty;
    public string? ExternalIdentifier { get; set; }
    public string Status { get; set; } = TenantLifecycle.ToStatusString(TenantStatus.Active);
    public string SubscribedPlan { get; set; } = "Starter";
    public int Capacity { get; set; } = 1000;
    public string State { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string StateRegistration { get; set; } = string.Empty;
    public decimal AreaInHectares { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? TechnicalOwnerName { get; set; }
    public string? TechnicalOwnerEmail { get; set; }
    public string? CommercialOwnerName { get; set; }
    public string? CommercialOwnerEmail { get; set; }
    public bool IsProtected { get; set; }
    public string? StatusReason { get; set; }
    public DateTime? StatusChangedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<UserTenant> UserTenants { get; set; } = new();
    public List<ProductionUnit> ProductionUnits { get; set; } = new();

    public TenantStatus GetStatusEnum() => TenantLifecycle.ParseStatus(Status);

    public Result ChangeStatus(TenantStatus target, string reason)
    {
        var validation = ValidateJustification(reason);
        if (validation.IsFailure)
        {
            return validation;
        }

        var current = GetStatusEnum();
        if (current == target)
        {
            return Result.Failure(TenancyErrors.AlreadyInStatus);
        }

        if (IsProtected && TenantLifecycle.IsRestrictedWhenProtected(target))
        {
            return Result.Failure(TenancyErrors.ProtectedTenant);
        }

        if (!TenantLifecycle.CanTransition(current, target))
        {
            return Result.Failure(TenancyErrors.InvalidTransition);
        }

        Status = TenantLifecycle.ToStatusString(target);
        StatusReason = reason.Trim();
        StatusChangedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
        return Result.Success();
    }

    public Result Suspend(string reason) => ChangeStatus(TenantStatus.Suspended, reason);

    public Result Reactivate(string reason) => ChangeStatus(TenantStatus.Active, reason);

    public Result Cancel(string reason) => ChangeStatus(TenantStatus.Cancelled, reason);

    public Result Archive(string reason) => ChangeStatus(TenantStatus.Archived, reason);

    public Result Activate(string reason) => ChangeStatus(TenantStatus.Active, reason);

    public Result MarkPastDue(string reason) => ChangeStatus(TenantStatus.PastDue, reason);

    public Result SetProtection(bool isProtected, string reason)
    {
        var validation = ValidateJustification(reason);
        if (validation.IsFailure)
        {
            return validation;
        }

        if (IsProtected == isProtected)
        {
            return Result.Failure(isProtected
                ? TenancyErrors.AlreadyProtected
                : TenancyErrors.AlreadyUnprotected);
        }

        IsProtected = isProtected;
        UpdatedAtUtc = DateTime.UtcNow;
        return Result.Success();
    }

    private static Result ValidateJustification(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < TenantLifecycle.MinJustificationLength)
        {
            return Result.Failure(TenancyErrors.JustificationRequired);
        }

        return Result.Success();
    }
}

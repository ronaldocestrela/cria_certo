namespace CriaCerto.Modules.Tenancy.Application.Contracts;

public record TenantDto(Guid Id, string Name, string State, string Type);

public record AuthResponse(
    string? Token,
    bool RequiresTenantSelection,
    List<TenantDto> AvailableTenants,
    Guid UserId,
    string FullName,
    string Email,
    bool IsPartner = false,
    string? RedirectUrl = null
);

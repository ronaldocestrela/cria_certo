namespace CriaCerto.Modules.Tenancy.Application.Abstractions;

public interface IPartnerRoleLookupService
{
    Task<bool> IsActiveCommercialPartnerAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> IsActiveCommercialPartnerAsync(Guid userId, string? email, CancellationToken cancellationToken = default)
        => IsActiveCommercialPartnerAsync(userId, cancellationToken);
}

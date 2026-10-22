namespace CriaCerto.Modules.Tenancy.Application.Abstractions;

public interface IPartnerRoleLookupService
{
    Task<bool> IsActiveCommercialPartnerAsync(Guid userId, CancellationToken cancellationToken = default);
}

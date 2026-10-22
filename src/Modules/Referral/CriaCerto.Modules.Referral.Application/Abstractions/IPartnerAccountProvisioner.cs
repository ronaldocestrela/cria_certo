using CriaCerto.BuildingBlocks.Abstractions.Results;

namespace CriaCerto.Modules.Referral.Application.Abstractions;

public interface IPartnerAccountProvisioner
{
    Task<Result<Guid>> ProvisionPartnerUserAsync(
        string fullName,
        string email,
        string? phoneNumber,
        string referralCode,
        CancellationToken cancellationToken = default);
}
